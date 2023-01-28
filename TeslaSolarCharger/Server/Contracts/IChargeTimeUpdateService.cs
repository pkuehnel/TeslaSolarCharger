using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Contracts;

public interface IChargeTimeUpdateService
{
    Task UpdateChargeTimes();
    TimeSpan CalculateTimeToReachMinSocAtFullSpeedCharge(Car car);
}
