using System.Net.WebSockets;
using TeslaSolarCharger.Server.Dtos;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IOcppWebSocketConnectionHandlingService
{

    void AddWebSocket(string chargePointId,
        WebSocket webSocket,
        TaskCompletionSource<object?> lifetimeTcs);

    void CleanupDeadConnections();

    Task<TResp> SendRequestAsync<TResp>(string chargePointIdentifier,
        string action,
        object requestPayload,
        CancellationToken outerCt);
}
