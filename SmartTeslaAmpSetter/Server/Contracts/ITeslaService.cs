namespace SmartTeslaAmpSetter.Server.Contracts;

public interface ITeslaService
{
    Task StartCharging(int carId, int startAmp, string? carState);
    Task WakeUpCar(int carId);
    Task StopCharging(int carId);
    Task SetAmp(int carId, int amps);
}