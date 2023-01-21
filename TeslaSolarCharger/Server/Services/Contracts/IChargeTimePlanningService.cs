using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IChargeTimePlanningService
{
    Task PlanChargeTimesForAllCars();
    Task UpdatePlannedChargingSlots(Car car);
}
