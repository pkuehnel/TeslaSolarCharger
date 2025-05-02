using System.Net.WebSockets;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IOcppWebSocketConnectionHandlingService
{

    void AddWebSocket(string chargePointId,
        WebSocket webSocket,
        TaskCompletionSource<object?> lifetimeTcs);

    /// <summary>Send raw bytes to a single charge‑point.</summary>
    Task SendTextAsync(string chargePointId, string message,
        CancellationToken ct = default);

    void CleanupDeadConnections();
}
