namespace TeslaSolarCharger.Server.Dtos;

public class DtoFleetTelemetryClient
{
    public string Vin { get; set; } = string.Empty;
    public DateTimeOffset ConnectedSince { get; set; }
}
