using TeslaSolarCharger.Server.Dtos.FleetTelemetry;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IFleetTelemetryConfigurationService
{
    Task<DtoGetFleetTelemetryConfiguration> GetFleetTelemetryConfiguration(string vin);
}
