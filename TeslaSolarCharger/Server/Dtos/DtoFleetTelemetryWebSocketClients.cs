using System.Net.WebSockets;

namespace TeslaSolarCharger.Server.Dtos;

public class DtoFleetTelemetryWebSocketClients
{
    public string Vin { get; set; }
    public ClientWebSocket WebSocketClient { get; set; }
    public DateTimeOffset LastReceivedHeartbeat { get; set; }
    public DateTimeOffset ConnectedSince { get; set; }
    public CancellationToken CancellationToken { get; set; }
}
