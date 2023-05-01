using TeslaSolarCharger.SharedBackend.Dtos;

namespace Plugins.SolarEdge.Contracts;

public interface ICurrentValuesService
{
    Task<int> GetCurrentPowerToGrid();
    Task<int> GetInverterPower();
    Task<int?> GetHomeBatterySoc();
    Task<int?> GetHomeBatteryPower();
    Task<DtoCurrentPvValues> GetCurrentPvValues();
}
