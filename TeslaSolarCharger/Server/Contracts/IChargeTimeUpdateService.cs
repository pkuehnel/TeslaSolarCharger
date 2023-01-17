using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Contracts;

public interface IChargeTimeUpdateService
{
    void UpdateChargeTimes();
    TimeSpan CalculateTimeToReachMinSocAtFullSpeedCharge(Car car);
}