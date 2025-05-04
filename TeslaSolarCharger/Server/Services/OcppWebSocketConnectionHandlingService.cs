using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.Ocpp;
using TeslaSolarCharger.Server.Services.Contracts;
using System.Text.Json.Serialization;
using System.Text.Json;
using TeslaSolarCharger.Server.Dtos.Ocpp.Generics;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace TeslaSolarCharger.Server.Services;

public sealed class OcppWebSocketConnectionHandlingService(
        ILogger<OcppWebSocketConnectionHandlingService> logger) : IOcppWebSocketConnectionHandlingService
{
    private readonly TimeSpan _sendTimeout = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _clientSideHeartbeatTimeout = TimeSpan.FromSeconds(120);
    private TimeSpan ClientSideHeartbeatConfigured => (_clientSideHeartbeatTimeout / 2) + _sendTimeout;

    private readonly ConcurrentDictionary<string, DtoOcppWebSocket> _connections = new();
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Converters =
        {
            new JsonStringEnumConverter(),
            new OcppArrayConverter(),
        },
    };

    public void AddWebSocket(string chargePointId,
                             WebSocket webSocket,
                             TaskCompletionSource<object?> lifetimeTcs)
    {
        logger.LogTrace("{method}({chargePointId})", nameof(AddWebSocket), chargePointId);
        RemoveWebSocket(chargePointId); // clear any stale entry first

        var dto = new DtoOcppWebSocket(chargePointId, webSocket, lifetimeTcs);

        if (_connections.TryAdd(chargePointId, dto))
        {
            logger.LogInformation("Added WebSocket connection for {chargePointId}", chargePointId);

            // fire‑and‑forget the receive loop
            _ = Task.Run(() => ReceiveLoopAsync(dto));
        }
        else
        {
            logger.LogWarning("Failed to add WebSocket connection for {chargePointId}", chargePointId);
        }
    }

    /// <summary>Send raw bytes to a single charge‑point.</summary>
    public Task SendTextAsync(string chargePointId, string message,
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
                var responseString = HandleIncoming(jsonMessage);
                await SendTextAsync(dto.ChargePointId, responseString, new CancellationTokenSource(_sendTimeout).Token);
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

    /// <summary>
    /// Inspects an incoming OCPP frame and returns the JSON to send back.
    /// </summary>
    private string HandleIncoming(string jsonMessage)
    {
        using var doc = JsonDocument.Parse(jsonMessage);
        var root = doc.RootElement;

        // 1) Sanity checks -----------------------------------------------------
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() < 3)
            return BuildError("FormationViolation", "Frame is not a valid OCPP array", null, null);

        var messageTypeId = root[0].GetInt32();
        var uniqueId = root[1].GetString()!;   // spec guarantees it’s a string

        // 2) Only CALLs (MessageTypeId == 2) need an immediate reply
        if (messageTypeId != (int)MessageTypeId.Call)
        {
            // For CALLRESULT/CALLERROR we have nothing to send back to the CP.
            return string.Empty;
        }

        var action = root[2].GetString()!;
        var payload = root.GetArrayLength() >= 4 ? root[3] : default;

        // 3) Dispatch by action -----------------------------------------------
        return action switch
        {
            "BootNotification" => HandleBootNotification(uniqueId, payload),
            "Heartbeat" => HandleHeartbeat(uniqueId),
            "StatusNotification" => HandleStatusNotification(uniqueId, payload),
            _ => BuildError("NotSupported", $"Action '{action}' not supported",
                uniqueId, null)
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
            IntervalSeconds = (int) ClientSideHeartbeatConfigured.TotalSeconds,                      // tell CP to heartbeat every 5 min
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
}
