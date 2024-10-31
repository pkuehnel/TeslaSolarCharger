namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IFleetTelemetryWebSocketService
{
    Task ReconnectWebSocketsForEnabledCars();
    Task DisconnectWebSocketsByVin(string vin);
}
