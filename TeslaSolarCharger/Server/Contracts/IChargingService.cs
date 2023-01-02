using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Contracts;

public interface IChargingService
{
    Task SetNewChargingValues();
    int CalculateAmpByPowerAndCar(int powerToControl, Car car);
    int CalculatePowerToControl();
}