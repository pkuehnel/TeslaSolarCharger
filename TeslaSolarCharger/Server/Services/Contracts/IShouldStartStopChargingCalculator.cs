using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IShouldStartStopChargingCalculator
{
    Task UpdateShouldStartStopChargingTimes(int targetPower, List<DtoLoadpoint> loadpoints);
}
