namespace TeslaSolarCharger.Server.Dtos.FleetTelemetry;

public class DtoFleetTelemetryErrorMessage
{
    public List<string> MissingKeyVins { get; set; } = new List<string>();
    public List<string> UnsupportedHardwareVins { get; set; } = new List<string>();
    public List<string> UnsupportedFirmwareVins { get; set; } = new List<string>();
}
