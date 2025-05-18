using Microsoft.EntityFrameworkCore;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.Ocpp;
using TeslaSolarCharger.Server.Services.Contracts;
using System.Text.Json.Serialization;
using System.Text.Json;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos.Ocpp.Generics;
using TeslaSolarCharger.Shared.Resources.Contracts;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace TeslaSolarCharger.Server.Services;

public sealed class OcppWebSocketConnectionHandlingService(
        ILogger<OcppWebSocketConnectionHandlingService> logger,
        IConstants constants,
        IServiceProvider serviceProvider) : IOcppWebSocketConnectionHandlingService
{
    private readonly TimeSpan _sendTimeout = TimeSpan.FromSeconds(5);
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
        RemoveWebSocket(chargePointId); // clear any stale entry first
        var dto = new DtoOcppWebSocket(chargePointId, webSocket, lifetimeTcs);

        if (_connections.TryAdd(chargePointId, dto))
        {
            logger.LogInformation("Added WebSocket connection for {chargePointId}", chargePointId);

            // fire‑and‑forget the receive loop
            _ = Task.Run(() => ReceiveLoopAsync(dto));

            using var scope = serviceProvider.CreateScope();
            var chargingStationConfigurationService = scope.ServiceProvider
                .GetRequiredService<IOcppChargingStationConfigurationService>();
            await chargingStationConfigurationService.AddChargingStationIfNotExisting(chargePointId, httpContextRequestAborted);
        }
        else
        {
            logger.LogWarning("Failed to add WebSocket connection for {chargePointId}", chargePointId);
        }
    }

    public void CleanupDeadConnections()
        => _ = Task.Run(() =>
        {
            var dead = _connections
                       .Where(kvp => kvp.Value.WebSocket.State != WebSocketState.Open)
                       .Select(kvp => kvp.Key)
                       .ToList();

            if (dead.Count == 0) return;

            logger.LogInformation("Cleaning up {dead} / {total} WS connections",
                                  dead.Count, _connections.Count);

            foreach (var id in dead)
                RemoveWebSocket(id);
        });



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
                                responseString = await HandleIncomingCall(dto.ChargePointId, uniqueMessageId, root);
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
            RemoveWebSocket(dto.ChargePointId);
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
    private async Task<string> HandleIncomingCall(string chargePointId, string uniqueMessageId, JsonElement root)
    {
        var action = root[2].GetString()!;
        var payload = root.GetArrayLength() >= 4 ? root[3] : default;

        // 3) Dispatch by action -----------------------------------------------
        return action switch
        {
            "BootNotification" => HandleBootNotification(uniqueMessageId, payload),
            "Heartbeat" => HandleHeartbeat(uniqueMessageId),
            "StatusNotification" => HandleStatusNotification(uniqueMessageId, payload),
            "StartTransaction" => await HandleStartTransaction(chargePointId, uniqueMessageId, payload),
            "StopTransaction" => await HandleStopTransaction(uniqueMessageId, payload),
            "MeterValues" => HandleMeterValues(uniqueMessageId, payload),
            "Authorize" => HandleAuthorize(uniqueMessageId, payload),
            _ => BuildError("NotSupported", $"Action '{action}' not supported",
                uniqueMessageId, null),
        };
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

    private string HandleStatusNotification(string uniqueId, JsonElement payload)
    {
        // a) Deserialize the request payload
        var req = payload.Deserialize<StatusNotificationRequest>(JsonOpts);

        // TODO: validate req & maybe persist CP information here … here at charge point level

        // b) Build the response payload
        var respPayload = new StatusNotificationResponse()
        {
        };

        // c) Wrap in an envelope and serialize
        var envelope = new CallResult<StatusNotificationResponse>(uniqueId, respPayload);
        return JsonSerializer.Serialize(envelope, JsonOpts);
    }
    private async Task<string> HandleStartTransaction(string chargePointId, string uniqueId, JsonElement payload)
    {
        // a) Deserialize the request payload
        var req = payload.Deserialize<StartTransactionRequest>(JsonOpts);

        if (req == default)
        {
            throw new OcppCallErrorException("FormationViolation", "StartTransaction payload is malformed");
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
            throw new OcppCallErrorException("FormationViolation", "StopTransaction payload is malformed");
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

    internal string HandleMeterValues(string uniqueId, JsonElement payload)
    {
        // a) Deserialize the request payload
        var req = payload.Deserialize<MeterValuesRequest>(JsonOpts);

        // TODO: validate req & maybe persist CP information here … here at charge point level

        // b) Build the response payload
        var respPayload = new MeterValuesResponse()
        {

        };

        // c) Wrap in an envelope and serialize
        var envelope = new CallResult<MeterValuesResponse>(uniqueId, respPayload);
        return JsonSerializer.Serialize(envelope, JsonOpts);
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
            RemoveWebSocket(dto.ChargePointId);
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
            RemoveWebSocket(dto.ChargePointId);
        }
    }

    private void RemoveWebSocket(string chargePointId)
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
    }

    public bool TryGetMessageType(int raw, out MessageTypeId result)
    {
        // passing ignoreCase: false is irrelevant for numbers,
        // but keeps the signature consistent for other enums
        return Enum.TryParse(raw.ToString(), ignoreCase: false, out result)
               && Enum.IsDefined(typeof(MessageTypeId), result);
    }
}

internal class OcppCallErrorException : Exception
{
    public string Code { get; }
    public string Description { get; }
    public OcppCallErrorException(string code, string desc)
    {
        Code = code;
        Description = desc;
    }
}
