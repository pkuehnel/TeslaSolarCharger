using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Contracts;

public interface IChargingService
{
    Task<int?> SetNewChargingValues();
    List<int> GetRelevantCarIds();
    int GetBatteryTargetChargingPower();
}
