namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IFleetTelemetryWebSocketService
{
    Task ReconnectWebSocketsForEnabledCars();
    bool IsClientConnected(string vin);
}
