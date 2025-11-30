using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IPowerToControlCalculationService
{
    int CalculatePowerToControl(List<DtoLoadPointWithCurrentChargingValues> currentChargingPower);

    int GetBatteryTargetChargingPower();

    bool HasTooLateChanges(DtoLoadPointWithCurrentChargingValues chargingLoadPoint, DateTimeOffset earliestAmpChange,
        DateTimeOffset earliestPlugin);
}
