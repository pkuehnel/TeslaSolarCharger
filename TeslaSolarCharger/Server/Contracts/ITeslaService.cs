namespace TeslaSolarCharger.Server.Contracts;

public interface ITeslaService
{
    Task StartCharging(int carId, int startAmp);
    Task StopCharging(int carId);
    Task SetAmp(int carId, int amps);
}
