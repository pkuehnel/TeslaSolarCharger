using TeslaSolarCharger.Server.Enums;

namespace TeslaSolarCharger.Server.Dtos.FleetTelemetry;

public class DtoFleetTelemetryConfigurationResult
{
    public bool Success { get; set; }
    public bool ConfigurationSent { get; set; }
    public string? ReconfigurationReason { get; set; }
    public string? ErrorMessage { get; set; }

    public TeslaFleetTelemetryConfigurationErrorType? ConfigurationErrorType { get; set; }
}
