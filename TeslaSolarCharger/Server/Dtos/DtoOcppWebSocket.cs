using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;

namespace TeslaSolarCharger.Server.Dtos;

public class DtoOcppWebSocket(
    string chargePointId,
    WebSocket webSocket,
    TaskCompletionSource<object?> lifetimeTsc)
{
    public string ChargePointId { get; set; } = chargePointId;
    public WebSocket WebSocket { get; set; } = webSocket;
    public bool FullyConfigured { get; set; }
    public TaskCompletionSource<object?> LifetimeTsc { get; set; } = lifetimeTsc;

    public ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> Pending { get; } = new();
}
