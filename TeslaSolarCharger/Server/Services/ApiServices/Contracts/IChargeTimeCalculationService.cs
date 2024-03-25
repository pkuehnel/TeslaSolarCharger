using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Services.ApiServices.Contracts;

public interface IChargeTimeCalculationService
{
    TimeSpan CalculateTimeToReachMinSocAtFullSpeedCharge(DtoCar dtoCar);
    void UpdateChargeTime(DtoCar dtoCar);
    Task PlanChargeTimesForAllCars();
    Task UpdatePlannedChargingSlots(DtoCar dtoCar);
    Task<bool> IsLatestTimeToReachSocAfterLatestKnownChargePrice(int carId);
}
