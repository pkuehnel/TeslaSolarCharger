using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Contracts;

public interface ITeslaService
{
    Task StartCharging(int carId, int startAmp, CarStateEnum? carState);
    Task WakeUpCar(int carId);
    Task StopCharging(int carId);
    Task SetAmp(int carId, int amps);
    Task SetScheduledCharging(int carId, DateTimeOffset? chargingStartTime);
}
