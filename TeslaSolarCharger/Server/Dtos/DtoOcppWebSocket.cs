using System.Net.WebSockets;

namespace TeslaSolarCharger.Server.Dtos;

public class DtoOcppWebSocket(
    string chargePointId,
    WebSocket webSocket,
    TaskCompletionSource<object?> lifetimeTsc)
{
    public string ChargePointId { get; set; } = chargePointId;
    public WebSocket WebSocket { get; set; } = webSocket;
    public TaskCompletionSource<object?> LifetimeTsc { get; set; } = lifetimeTsc;
}
