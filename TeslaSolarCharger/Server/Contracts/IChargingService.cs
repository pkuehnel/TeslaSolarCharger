using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Contracts;

public interface IChargingService
{
    Task SetNewChargingValues();
    int CalculateAmpByPowerAndCar(int powerToControl, Car car);
    Task<int> CalculatePowerToControl(bool calculateAverage);
    List<int> GetRelevantCarIds();
    int GetBatteryTargetChargingPower();
    Task UpdatePlannedChargingSlots(Car car);
    Task<bool> IsLatestTimeToReachSocAfterLatestKnownChargePrice(int carId);
}
