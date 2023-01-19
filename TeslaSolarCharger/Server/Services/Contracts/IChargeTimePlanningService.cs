using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IChargeTimePlanningService
{
    void PlanChargeTimesForAllCars();
    void UpdatePlannedChargingSlots(Car car);
}
