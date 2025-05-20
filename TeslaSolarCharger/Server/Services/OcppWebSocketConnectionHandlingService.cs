using Microsoft.EntityFrameworkCore;
using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.Ocpp;
using TeslaSolarCharger.Server.Dtos.Ocpp.Generics;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace TeslaSolarCharger.Server.Services;

public sealed class OcppWebSocketConnectionHandlingService(
        ILogger<OcppWebSocketConnectionHandlingService> logger,
        IConstants constants,
        IServiceProvider serviceProvider,
        ISettings settings,
        IDateTimeProvider dateTimeProvider) : IOcppWebSocketConnectionHandlingService
{
    private readonly TimeSpan _sendTimeout = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _messageHandlingTimeout = TimeSpan.FromSeconds(20);
    private TimeSpan RoundTripTimeout => _sendTimeout * 2;
    private readonly TimeSpan _clientSideHeartbeatTimeout = TimeSpan.FromSeconds(120);
    private TimeSpan ClientSideHeartbeatConfigured => (_clientSideHeartbeatTimeout / 2) + _sendTimeout;

    private readonly ConcurrentDictionary<string, DtoOcppWebSocket> _connections = new();
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

        Converters =
        {
            new JsonStringEnumConverter(),
            new OcppArrayConverter(),
        },
    };

    public async Task AddWebSocket(string chargePointId,
        WebSocket webSocket,
        TaskCompletionSource<object?> lifetimeTcs, CancellationToken httpContextRequestAborted)
    {
        logger.LogTrace("{method}({chargePointId})", nameof(AddWebSocket), chargePointId);
        await RemoveWebSocket(chargePointId); // clear any stale entry first
        var dto = new DtoOcppWebSocket(chargePointId, webSocket, lifetimeTcs);

        if (_connections.TryAdd(chargePointId, dto))
        {
            logger.LogInformation("Added WebSocket connection for {chargePointId}", chargePointId);

            // fire‑and‑forget the receive loop before everything else as is required to configure charge point
            _ = Task.Run(() => ReceiveLoopAsync(dto));

            using var scope = serviceProvider.CreateScope();
            var chargingStationConfigurationService = scope.ServiceProvider
                .GetRequiredService<IOcppChargingStationConfigurationService>();
            await chargingStationConfigurationService.AddChargingStationIfNotExisting(chargePointId, httpContextRequestAborted);
            var chargingConnectorIds = await GetChargingConnectorIds(chargePointId);
            foreach (var chargingConnectorId in chargingConnectorIds)
            {
                settings.OcppConnectorStates.TryAdd(chargingConnectorId, new());
            }
        }
        else
        {
            logger.LogWarning("Failed to add WebSocket connection for {chargePointId}", chargePointId);
        }
    }

    private async Task<System.Collections.Generic.HashSet<int>> GetChargingConnectorIds(string chargePointId)
    {
        logger.LogTrace("{method}({chargePointId})", nameof(GetChargingConnectorIds), chargePointId);
        using var scope = serviceProvider.CreateScope();
        var scopedContext = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        var connectorIds = await scopedContext.OcppChargingStationConnectors
            .Where(c => c.OcppChargingStation.ChargepointId == chargePointId)
            .Select(c => c.Id)
            .ToHashSetAsync().ConfigureAwait(false);
        return connectorIds;
    }


    private async Task ReceiveLoopAsync(DtoOcppWebSocket dto)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(4 * 1024);

        var watchdog = new CancellationTokenSource(_clientSideHeartbeatTimeout);
        var linked = CancellationTokenSource.CreateLinkedTokenSource(watchdog.Token);

        try
        {
            while (dto.WebSocket.State == WebSocketState.Open && !linked.IsCancellationRequested)
            {
                var result = await dto.WebSocket.ReceiveAsync(new(buffer), linked.Token);
                var jsonMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                logger.LogTrace("Received from {chargePointId}: {message}", dto.ChargePointId, jsonMessage);
                var doc = JsonDocument.Parse(jsonMessage);
                var root = doc.RootElement;
                string? responseString = null;
                // 1) Sanity checks -----------------------------------------------------
                if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() < 3)
                {
                    responseString = BuildError("FormationViolation", "Frame is not a valid OCPP array", null, null);
                }
                else
                {
                    var messageTypeIdInt = root[0].GetInt32();
                    var uniqueMessageId = root[1].GetString()!;
                    if (!TryGetMessageType(messageTypeIdInt, out var messageTypeId))
                    {
                        responseString = BuildError("FormationViolation", "Message Type ID is undefined", uniqueMessageId, null);
                    }
                    else
                    {
                        switch (messageTypeId)
                        {
                            case MessageTypeId.Call:
                                // fire‐and‐forget the handler _and_ its SendTextAsync
                                _ = Task.Run(async () =>
                                {
                                    var resp = await HandleIncomingCall(dto.ChargePointId, uniqueMessageId, root, new CancellationTokenSource(_messageHandlingTimeout).Token);
                                    if (!string.IsNullOrEmpty(resp))
                                    {
                                        await SendTextAsync(dto.ChargePointId,
                                            resp,
                                            new CancellationTokenSource(_sendTimeout).Token);
                                    }
                                }, linked.Token);
                                break;
                            case MessageTypeId.CallResult:
                                if (dto.Pending.TryRemove(uniqueMessageId, out var tcsOk))
                                {
                                    // index 2 holds the payload for CALLRESULT
                                    tcsOk.TrySetResult(root[2]);
                                }
                                break;
                            case MessageTypeId.CallError:
                                if (dto.Pending.TryRemove(uniqueMessageId, out var tcsErr))
                                {
                                    var code = root[2].GetString();
                                    var desc = root[3].GetString();
                                    tcsErr.TrySetException(new OcppCallErrorException(code!, desc!));
                                }
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }


                }

                if (!string.IsNullOrEmpty(responseString))
                {
                    await SendTextAsync(dto.ChargePointId, responseString, new CancellationTokenSource(_sendTimeout).Token);
                }
                // reset heartbeat timer whenever something arrives
                watchdog.CancelAfter(_clientSideHeartbeatTimeout);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (watchdog.IsCancellationRequested)
        {
            logger.LogWarning("Heartbeat timeout for {chargePointId}", dto.ChargePointId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Receive loop crashed for {chargePointId}", dto.ChargePointId);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            await RemoveWebSocket(dto.ChargePointId);
            dto.LifetimeTsc.TrySetResult(null);
            watchdog.Dispose();
            linked.Dispose();
        }
    }

    public async Task<TResp> SendRequestAsync<TResp>(string chargePointIdentifier,
        string action,
        object requestPayload,
        CancellationToken outerCt)
    {
        // 1) Build array frame [ 2, "<uid>", "Action", {…payload…} ]
        var uid = Guid.NewGuid().ToString("N");
        object envelope = new[] { (int)MessageTypeId.Call, uid, action, requestPayload };

        var json = JsonSerializer.Serialize(envelope, JsonOpts);

        // 2) Prepare a TCS that will complete when the CP answers
        var tcs = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var dto = _connections[chargePointIdentifier];
        if (!dto.Pending.TryAdd(uid, tcs))
            throw new InvalidOperationException("UID collision—should never happen");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
        cts.CancelAfter(RoundTripTimeout);           // response timeout

        try
        {
            await SendTextAsync(dto.ChargePointId, json, cts.Token);

            // 3) Wait until ReceiveLoop completes the TCS
            await using (cts.Token.Register(() => tcs.TrySetCanceled(cts.Token),
                             useSynchronizationContext: false).ConfigureAwait(false))
            {
                var raw = await tcs.Task;             // ← suspends here
                return raw.Deserialize<TResp>(JsonOpts)!;     // typed payload back
            }
        }
        finally
        {
            dto.Pending.TryRemove(uid, out _);
        }
    }

    private Task SendTextAsync(string chargePointId, string message,
        CancellationToken ct = default)
    {
        logger.LogTrace("{method}({chargePointId}, {message})", nameof(SendTextAsync), chargePointId, message);
        if (!_connections.TryGetValue(chargePointId, out var dto))
        {
            logger.LogWarning("No open WS for {chargePointId}", chargePointId);
            return Task.CompletedTask;
        }
        var payload = Encoding.UTF8.GetBytes(message);
        return SendInternalAsync(dto, payload, WebSocketMessageType.Text, ct);
    }

    /// <summary>
    /// Inspects an incoming OCPP frame and returns the JSON to send back.
    /// </summary>
    private async Task<string> HandleIncomingCall(string chargePointId, string uniqueMessageId, JsonElement root, CancellationToken cancellationToken)
    {
        var action = root[2].GetString()!;
        var payload = root.GetArrayLength() >= 4 ? root[3] : default;

        try
        {
            return action switch
            {
                "BootNotification" => HandleBootNotification(uniqueMessageId, payload),
                "Heartbeat" => HandleHeartbeat(uniqueMessageId),
                "StatusNotification" => await HandleStatusNotification(chargePointId, uniqueMessageId, payload, cancellationToken),
                "StartTransaction" => await HandleStartTransaction(chargePointId, uniqueMessageId, payload),
                "StopTransaction" => await HandleStopTransaction(uniqueMessageId, payload),
                "MeterValues" => await HandleMeterValues(chargePointId, uniqueMessageId, payload),
                "Authorize" => HandleAuthorize(uniqueMessageId, payload),
                _ => BuildError("NotSupported", $"Action '{action}' not supported",
                    uniqueMessageId, null),
            };
        }
        catch (OcppCallErrorException ex)
        {
            return BuildError(ex.ToString(), ex.Description, null, null);
        }
    }

    private string HandleBootNotification(string uniqueId, JsonElement payload)
    {
        // a) Deserialize the request payload
        var req = payload.Deserialize<BootNotificationRequest>(JsonOpts);

        // TODO: validate req & maybe persist CP information here … but here is charging station only

        // b) Build the response payload
        var respPayload = new BootNotificationResponse
        {
            Status = RegistrationStatus.Accepted,
            CurrentTimeUtc = DateTime.UtcNow,
            IntervalSeconds = (int)ClientSideHeartbeatConfigured.TotalSeconds,                      // tell CP to heartbeat every 5 min
        };

        // c) Wrap in an envelope and serialize
        var envelope = new CallResult<BootNotificationResponse>(uniqueId, respPayload);
        return JsonSerializer.Serialize(envelope, JsonOpts);
    }

    private string HandleHeartbeat(string uniqueId)
    {
        var respPayload = new HeartbeatResponse
        {
            CurrentTimeUtc = DateTime.UtcNow,
        };

        var envelope = new CallResult<HeartbeatResponse>(uniqueId, respPayload);
        return JsonSerializer.Serialize(envelope, JsonOpts);
    }

    private async Task<string> HandleStatusNotification(string chargePointId, string uniqueId, JsonElement payload,
        CancellationToken cancellationToken)
    {
        // a) Deserialize the request payload
        var req = payload.Deserialize<StatusNotificationRequest>(JsonOpts);
        if (req == default)
        {
            throw new OcppCallErrorException(CallErrorCode.FormationViolation);
        }


        using var scope = serviceProvider.CreateScope();
        var scopedContext = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        var chargingConnectorQuery = scopedContext.OcppChargingStationConnectors.AsQueryable()
            .Where(c => c.OcppChargingStation.ChargepointId == chargePointId);

        //Connector ID 0 means it is not related to a specific charge point but to all of them, so limit to connector only if not 0
        if (req.ConnectorId != 0)
        {
            chargingConnectorQuery = chargingConnectorQuery.Where(c => c.ConnectorId == req.ConnectorId);
        }

        var chargingConnectorIds = await chargingConnectorQuery
            .Select(c => c.Id)
            .ToHashSetAsync(cancellationToken: cancellationToken);


        if (chargingConnectorIds.Count < 1)
        {
            throw new OcppCallErrorException(CallErrorCode.PropertyConstraintViolation,
                "The connector ID does not exist for charging station.");
        }

        foreach (var databaseChargePointId in chargingConnectorIds)
        {
            var ocppConnectorState = await GetConnectorStateAsync(databaseChargePointId, cancellationToken);
            var timestamp = req.Timestamp == default
                ? dateTimeProvider.DateTimeOffSetUtcNow()
                : new(req.Timestamp.Value, TimeSpan.Zero);
            UpdateCacheBasedOnState(databaseChargePointId, req.Status, ocppConnectorState, timestamp);
            //Only add value if with this timestamp it was updated
            if (timestamp == ocppConnectorState.IsConnected.Timestamp)
            {
                scopedContext.OcppChargingStationConnectorValueLogs.Add(new()
                {
                    Timestamp = ocppConnectorState.IsConnected.Timestamp,
                    Type = OcppChargingStationConnectorValueType.IsPluggedIn,
                    BooleanValue = ocppConnectorState.IsConnected.Value,
                    OcppChargingStationConnectorId = databaseChargePointId,
                });
            }
            
        }
        await scopedContext.SaveChangesAsync(cancellationToken);

        // b) Build the response payload
        var respPayload = new StatusNotificationResponse()
        {
        };

        // c) Wrap in an envelope and serialize
        var envelope = new CallResult<StatusNotificationResponse>(uniqueId, respPayload);
        return JsonSerializer.Serialize(envelope, JsonOpts);
    }

    private async Task<DtoOcppConnectorState> GetConnectorStateAsync(int databaseChargePointId, CancellationToken ct = default)
    {
        DtoOcppConnectorState? state;
        while (!settings.OcppConnectorStates.TryGetValue(databaseChargePointId, out state!))
        {
            logger.LogDebug("Waiting for connector state for ChargePoint {Id}", databaseChargePointId);
            await Task.Delay(100, ct);   // poll every 100 ms (tweak as needed)
        }
        return state;
    }

    private void UpdateCacheBasedOnState(int databaseChargePointId, ChargePointStatus reqStatus,
        DtoOcppConnectorState ocppConnectorState, DateTimeOffset timestamp)
    {
        logger.LogTrace("{method}({id}, {status})", nameof(UpdateCacheBasedOnState), databaseChargePointId, reqStatus);
        logger.LogTrace("Cache value before update: {@cache}", ocppConnectorState);
        switch (reqStatus)
        {
            case ChargePointStatus.Available:
                ocppConnectorState.IsConnected = new(timestamp, false);
                ocppConnectorState.UpdateIsCharging(timestamp, false);
                ocppConnectorState.IsCarFullyCharged = new(timestamp, null);
                ocppConnectorState.ChargingPower = new(timestamp, 0);
                break;
            case ChargePointStatus.Preparing:
                ocppConnectorState.IsConnected = new(timestamp, true);
                ocppConnectorState.UpdateIsCharging(timestamp, false);
                ocppConnectorState.IsCarFullyCharged = new(timestamp, null);
                ocppConnectorState.ChargingPower = new(timestamp, 0);
                break;
            case ChargePointStatus.Charging:
                ocppConnectorState.IsConnected = new(timestamp, true);
                ocppConnectorState.UpdateIsCharging(timestamp, true);
                ocppConnectorState.IsCarFullyCharged = new(timestamp, false);
                break;
            case ChargePointStatus.SuspendedEVSE:
                ocppConnectorState.IsConnected = new(timestamp, true);
                ocppConnectorState.UpdateIsCharging(timestamp, false);
                ocppConnectorState.ChargingPower = new(timestamp, 0);
                break;
            case ChargePointStatus.SuspendedEV:
                ocppConnectorState.IsConnected = new(timestamp, true);
                ocppConnectorState.UpdateIsCharging(timestamp, false);
                ocppConnectorState.IsCarFullyCharged = new(timestamp, true);
                ocppConnectorState.ChargingPower = new(timestamp, 0);
                break;
            case ChargePointStatus.Finishing:
                ocppConnectorState.IsConnected = new(timestamp, true);
                ocppConnectorState.UpdateIsCharging(timestamp, false);
                ocppConnectorState.ChargingPower = new(timestamp, 0);
                break;
            default:
                logger.LogWarning("Can not handle chargepoint status {state}", reqStatus);
                break;
        }
        logger.LogTrace("Updated cache to {@cache}", ocppConnectorState);
    }

    private async Task<string> HandleStartTransaction(string chargePointId, string uniqueId, JsonElement payload)
    {
        // a) Deserialize the request payload
        var req = payload.Deserialize<StartTransactionRequest>(JsonOpts);

        if (req == default)
        {
            throw new OcppCallErrorException(CallErrorCode.FormationViolation);
        }

        using var scope = serviceProvider.CreateScope();
        var scopedContext = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        var openTransactions = await scopedContext.OcppTransactions
            .Where(t => t.ChargingStationConnector.OcppChargingStation.ChargepointId == chargePointId
                        && t.ChargingStationConnector.ConnectorId == req.ConnectorId
                        && t.EndDate == null)
            .ToListAsync().ConfigureAwait(false);
        var stopDate = new DateTimeOffset(req.TimestampUtc, TimeSpan.Zero).AddSeconds(-1);
        foreach (var openTransaction in openTransactions)
        {
            openTransaction.EndDate = stopDate;
        }
        await scopedContext.SaveChangesAsync();

        var chargingStationConnector = await scopedContext.OcppChargingStationConnectors
            .Where(c => c.OcppChargingStation.ChargepointId == chargePointId
                        && c.ConnectorId == req.ConnectorId)
            .FirstAsync().ConfigureAwait(false);
        var ocppTransaction = new OcppTransaction()
        {
            StartDate = new DateTimeOffset(req.TimestampUtc, TimeSpan.Zero),
            ChargingStationConnectorId = chargingStationConnector.Id,
        };
        scopedContext.OcppTransactions.Add(ocppTransaction);
        await scopedContext.SaveChangesAsync().ConfigureAwait(false);

        // b) Build the response payload
        var respPayload = new StartTransactionResponse()
        {
            TransactionId = ocppTransaction.Id,
            IdTagInfo = new IdTagInfo
            {
                Status = AuthorizationStatus.Accepted,
            },
        };

        // c) Wrap in an envelope and serialize
        var envelope = new CallResult<StartTransactionResponse>(uniqueId, respPayload);
        return JsonSerializer.Serialize(envelope, JsonOpts);
    }

    private async Task<string> HandleStopTransaction(string uniqueId, JsonElement payload)
    {
        // a) Deserialize the request payload
        var req = payload.Deserialize<StopTransactionRequest>(JsonOpts);

        if (req == default)
        {
            throw new OcppCallErrorException(CallErrorCode.FormationViolation);
        }

        using var scope = serviceProvider.CreateScope();
        var scopedContext = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        var ocppTransaction = await scopedContext.OcppTransactions
            .FirstAsync(t => t.Id == req.TransactionId);
        ocppTransaction.EndDate = new DateTimeOffset(req.TimestampUtc, TimeSpan.Zero);
        await scopedContext.SaveChangesAsync().ConfigureAwait(false);

        // b) Build the response payload
        var respPayload = new StopTransactionResponse()
        {
            IdTagInfo = new IdTagInfo
            {
                Status = AuthorizationStatus.Accepted,
            },
        };

        // c) Wrap in an envelope and serialize
        var envelope = new CallResult<StopTransactionResponse>(uniqueId, respPayload);
        return JsonSerializer.Serialize(envelope, JsonOpts);
    }

    internal async Task<string> HandleMeterValues(string chargePointId, string uniqueId, JsonElement payload)
    {
        // a) Deserialize the request payload
        var req = payload.Deserialize<MeterValuesRequest>(JsonOpts);

        if (req == default)
        {
            throw new OcppCallErrorException(CallErrorCode.FormationViolation);
        }

        using var scope = serviceProvider.CreateScope();
        var scopedContext = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();

        var chargePointQuery = scopedContext.OcppChargingStationConnectors.AsQueryable()
            .Where(c => c.OcppChargingStation.ChargepointId == chargePointId);
        if (req.ConnectorId != 0)
        {
            chargePointQuery = chargePointQuery.Where(c => c.ConnectorId == req.ConnectorId);
        }
        var chargingConnectorIds = await chargePointQuery
            .Select(c => c.Id)
            .ToHashSetAsync().ConfigureAwait(false);
        if (chargingConnectorIds.Count < 1)
        {
            throw new OcppCallErrorException(CallErrorCode.PropertyConstraintViolation,
                "The connector ID does not exist for charging station.");
        }

        var latestMeterValue = req.MeterValue.OrderByDescending(m => m.Timestamp).First();
        foreach (var chargingConnectorId in chargingConnectorIds)
        {
            if (!settings.OcppConnectorStates.TryGetValue(chargingConnectorId, out var ocppConnector))
            {
                logger.LogWarning("Can not find a cached OCPP connector with ID {chargingConnectorId}. Do not update values", chargingConnectorId);
                continue;
            }
            SetVoltage(latestMeterValue.SampledValue, latestMeterValue.Timestamp, ocppConnector);
            if (chargingConnectorIds.Count > 1)
            {
                logger.LogWarning("The charging station has more than one charge point, only setting voltage");
                continue;
            }
            SetPower(latestMeterValue.SampledValue, latestMeterValue.Timestamp, ocppConnector);
            SetCurrent(latestMeterValue.SampledValue, latestMeterValue.Timestamp, ocppConnector);
            SetPhases(latestMeterValue.SampledValue, latestMeterValue.Timestamp, ocppConnector);
        }

        // b) Build the response payload
        var respPayload = new MeterValuesResponse()
        {

        };

        // c) Wrap in an envelope and serialize
        var envelope = new CallResult<MeterValuesResponse>(uniqueId, respPayload);
        return JsonSerializer.Serialize(envelope, JsonOpts);
    }

    private void SetPhases(List<SampledValue> sampledValue, DateTime timestamp, DtoOcppConnectorState ocppConnector)
    {
        logger.LogTrace("{method}({@values}, {ocppConnector})", nameof(SetPhases), sampledValue, ocppConnector);
        var relevantValues = sampledValue
            .Where(v => v.Measurand == Measurand.CurrentImport && v.Phase != default)
            .GroupBy(v => v.Phase)
            .Select(g => g.First())
            .ToList();
        if (relevantValues.Count < 1)
        {
            logger.LogWarning("Can not set phases as no phases values are present");
            return;
        }
        var maxCurrent = relevantValues.Select(v => decimal.Parse(v.Value, CultureInfo.InvariantCulture)).Max();
        var phaseCount = relevantValues
            .Count(v => decimal.Parse(v.Value, CultureInfo.InvariantCulture) > (maxCurrent * 0.8m));
        ocppConnector.PhaseCount = new(new(timestamp, TimeSpan.Zero), phaseCount);
    }

    private void SetCurrent(List<SampledValue> sampledValue, DateTime timestamp, DtoOcppConnectorState ocppConnector)
    {
        logger.LogTrace("{method}({@values}, {ocppConnector})", nameof(SetCurrent), sampledValue, ocppConnector);
        var relevantValues = sampledValue.Where(v => v.Measurand == Measurand.CurrentImport).ToList();
        if (relevantValues.Count < 1)
        {
            logger.LogWarning("Can not set current as no current values are present");
            return;
        }
        var maxCurrent = relevantValues.Select(v => decimal.Parse(v.Value, CultureInfo.InvariantCulture)).Max();
        ocppConnector.ChargingCurrent = new(new(timestamp, TimeSpan.Zero), maxCurrent);
    }

    private void SetPower(List<SampledValue> sampledValue, DateTime timestamp, DtoOcppConnectorState ocppConnector)
    {
        logger.LogTrace("{method}({@values}, {ocppConnector})", nameof(SetPower), sampledValue, ocppConnector);
        var relevantValues = sampledValue.Where(v => v.Measurand == Measurand.PowerActiveImport).ToList();
        if (relevantValues.Count < 1)
        {
            logger.LogWarning("Can not set power as not power values are present");
            return;
        }
        var combinedPower = relevantValues.Where(v => v.Phase == default).ToList();
        if (combinedPower.Count == 1)
        {
            logger.LogTrace("Using combined power as power");
            var element = combinedPower.First();
            var correctionFactor = element.Unit == UnitOfMeasure.KW ? 1000 : 1;
            ocppConnector.ChargingPower = new(new(timestamp, TimeSpan.Zero), Convert.ToInt32(decimal.Parse(element.Value, CultureInfo.InvariantCulture) * correctionFactor));
            return;
        }
        var phaseBasedPowers = relevantValues.Where(v => v.Phase != default).ToList();
        logger.LogTrace("Using sum of phases as power");
        ocppConnector.ChargingPower = new(new(timestamp, TimeSpan.Zero), Convert.ToInt32(phaseBasedPowers.Select(v => decimal.Parse(v.Value, CultureInfo.InvariantCulture) * (v.Unit == UnitOfMeasure.KW ? 1000 : 1)).Sum()));
    }

    private void SetVoltage(List<SampledValue> sampledValue, DateTime timestamp, DtoOcppConnectorState ocppConnector)
    {
        logger.LogTrace("{method}({@values}, {ocppConnector})", nameof(SetVoltage), sampledValue, ocppConnector);
        var voltageValues = sampledValue.Where(v => v.Measurand == Measurand.Voltage).ToList();
        if (voltageValues.Count < 1)
        {
            logger.LogWarning("Can not set voltage value as not present");
            return;
        }
        var decimalVoltageValues = voltageValues.Select(v => decimal.Parse(v.Value, CultureInfo.InvariantCulture)).ToList();
        var maxVoltage = decimalVoltageValues.Max();
        var maxVoltageDifference = 20;
        decimalVoltageValues.RemoveAll(v => v < (maxVoltage - maxVoltageDifference));
        ocppConnector.ChargingVoltage = new(new(timestamp, TimeSpan.Zero), decimalVoltageValues.Average());
    }

    private string HandleAuthorize(string uniqueId, JsonElement payload)
    {
        // a) Deserialize the request payload
        var req = payload.Deserialize<AuthorizeRequest>(JsonOpts);

        // TODO: validate req & maybe persist CP information here … here at charge point level

        // b) Build the response payload
        var respPayload = new AuthorizeResponse()
        {
            IdTagInfo = new IdTagInfo
            {
                Status = (req != default && string.Equals(req.IdTag, constants.DefaultIdTag)) ? AuthorizationStatus.Accepted : AuthorizationStatus.Invalid,
            },
        };

        // c) Wrap in an envelope and serialize
        var envelope = new CallResult<AuthorizeResponse>(uniqueId, respPayload);
        return JsonSerializer.Serialize(envelope, JsonOpts);
    }

    private string BuildError(string code, string description,
        string? uniqueId, object? details)
    {
        var error = new CallError(uniqueId ?? Guid.NewGuid().ToString("N"),
            code, description, details);
        return JsonSerializer.Serialize(error, JsonOpts);
    }

    private async Task SendInternalAsync(DtoOcppWebSocket dto,
                                         byte[] payload,
                                         WebSocketMessageType type,
                                         CancellationToken outerCt)
    {
        if (dto.WebSocket.State != WebSocketState.Open)
        {
            await RemoveWebSocket(dto.ChargePointId);
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
        cts.CancelAfter(_sendTimeout);

        try
        {
            await dto.WebSocket.SendAsync(payload, type, true, cts.Token);
            logger.LogTrace("Sent {bytes} bytes to {chargePointId}", payload.Length, dto.ChargePointId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending to {chargePointId}", dto.ChargePointId);
            await RemoveWebSocket(dto.ChargePointId);
        }
    }

    private async Task RemoveWebSocket(string chargePointId)
    {
        if (_connections.TryRemove(chargePointId, out var dto))
        {
            logger.LogInformation("Removed WS for {chargePointId}", chargePointId);
            dto.LifetimeTsc.TrySetResult(null);

            try
            {
                if (dto.WebSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    _ = dto.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                                 "Server closing",
                                                 CancellationToken.None);
                }
                dto.WebSocket.Dispose();
            }
            catch { /* swallow */ }
        }

        using var scope = serviceProvider.CreateScope();
        var scopedContext = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        var chargingConnectorIds = await scopedContext.OcppChargingStationConnectors
            .Where(c => c.OcppChargingStation.ChargepointId == chargePointId)
            .Select(c => c.Id)
            .ToHashSetAsync();
        foreach (var chargingConnectorId in chargingConnectorIds)
        {
            settings.OcppConnectorStates.Remove(chargingConnectorId, out _);
        }
    }

    private bool TryGetMessageType(int raw, out MessageTypeId result)
    {
        // passing ignoreCase: false is irrelevant for numbers,
        // but keeps the signature consistent for other enums
        return Enum.TryParse(raw.ToString(), ignoreCase: false, out result)
               && Enum.IsDefined(typeof(MessageTypeId), result);
    }
}


