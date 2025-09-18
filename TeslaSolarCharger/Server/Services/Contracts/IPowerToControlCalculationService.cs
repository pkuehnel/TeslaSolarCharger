using TeslaSolarCharger.Server.Helper.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IPowerToControlCalculationService
{
    Task<int> CalculatePowerToControl(List<DtoLoadPointWithCurrentChargingValues> currentChargingPower,
        INotChargingWithExpectedPowerReasonHelper notChargingWithExpectedPowerReasonHelper, CancellationToken cancellationToken);

    int GetBatteryTargetChargingPower();

    bool HasTooLateChanges(DtoLoadPointWithCurrentChargingValues chargingLoadPoint, DateTimeOffset earliestAmpChange,
        DateTimeOffset earliestPlugin);
}
