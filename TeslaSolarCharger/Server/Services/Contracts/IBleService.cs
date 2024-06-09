using TeslaSolarCharger.Server.Dtos.Ble;
using TeslaSolarCharger.Shared.Dtos.Ble;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IBleService
{
    Task<DtoBleResult> StartCharging(string vin);
    Task<DtoBleResult> StopCharging(string vin);
    Task<DtoBleResult> SetAmp(string vin, int amps);
    Task<DtoBleResult> FlashLights(string vin);
    Task<DtoBleResult> PairKey(string vin);
}
