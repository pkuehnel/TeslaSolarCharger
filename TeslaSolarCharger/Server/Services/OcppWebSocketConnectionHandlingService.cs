using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Services;

public sealed class OcppWebSocketConnectionHandlingService(
        ILogger<OcppWebSocketConnectionHandlingService> logger) : IOcppWebSocketConnectionHandlingService
{
    private readonly TimeSpan _sendTimeout = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _clientSideHeartbeatTimeout = TimeSpan.FromSeconds(120);

    private readonly ConcurrentDictionary<string, DtoOcppWebSocket> _connections = new();

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
                logger.LogTrace("Received from {chargePointId}: {message}",dto.ChargePointId, jsonMessage);
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
