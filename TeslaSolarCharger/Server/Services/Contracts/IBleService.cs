using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IBleService
{
    Task StartCharging(string vin);
    Task StopCharging(string vin);
    Task SetAmp(string vin, int amps);
}
