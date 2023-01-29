using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Services.ApiServices.Contracts;

public interface IChargeTimeCalculationService
{
    TimeSpan CalculateTimeToReachMinSocAtFullSpeedCharge(Car car);
    void UpdateChargeTime(Car car);
}
