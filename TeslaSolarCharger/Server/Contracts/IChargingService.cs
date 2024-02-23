using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Contracts;

public interface IChargingService
{
    Task SetNewChargingValues();
    int CalculateAmpByPowerAndCar(int powerToControl, DtoCar dtoCar);
    int CalculatePowerToControl(bool calculateAverage);
    List<int> GetRelevantCarIds();
    int GetBatteryTargetChargingPower();
}
