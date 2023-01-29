using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Services.ApiServices.Contracts;

public interface IChargeTimeCalculationService
{
    TimeSpan CalculateTimeToReachMinSocAtFullSpeedCharge(Car car);
    void UpdateChargeTime(Car car);
    Task PlanChargeTimesForAllCars();
    Task UpdatePlannedChargingSlots(Car car);
    Task<bool> IsLatestTimeToReachSocAfterLatestKnownChargePrice(int carId);
}
