using System.Net.WebSockets;
using TeslaSolarCharger.Server.Dtos;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IOcppWebSocketConnectionHandlingService
{

    Task AddWebSocket(string chargePointId,
        WebSocket webSocket,
        TaskCompletionSource<object?> lifetimeTcs, CancellationToken httpContextRequestAborted);

    void CleanupDeadConnections();

    Task<TResp> SendRequestAsync<TResp>(string chargePointIdentifier,
        string action,
        object requestPayload,
        CancellationToken outerCt);
}
