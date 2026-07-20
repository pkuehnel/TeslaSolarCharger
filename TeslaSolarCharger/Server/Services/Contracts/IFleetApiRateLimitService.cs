using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IFleetApiRateLimitService
{
    DateTime? GetNextAllowedUtc(DtoCar car);
    void RecordSuccessfulCommand(DtoCar car);
}
