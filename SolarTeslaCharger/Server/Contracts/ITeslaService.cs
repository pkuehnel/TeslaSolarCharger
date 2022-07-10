using SolarTeslaCharger.Shared.Enums;

namespace SolarTeslaCharger.Server.Contracts;

public interface ITeslaService
{
    Task StartCharging(int carId, int startAmp, CarState? carState);
    Task WakeUpCar(int carId);
    Task StopCharging(int carId);
    Task SetAmp(int carId, int amps);
}