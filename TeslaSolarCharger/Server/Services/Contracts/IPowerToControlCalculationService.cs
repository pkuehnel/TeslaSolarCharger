using TeslaSolarCharger.Server.Helper.Contracts;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IPowerToControlCalculationService
{
    Task<int> CalculatePowerToControl(int currentChargingPower,
        INotChargingWithExpectedPowerReasonHelper notChargingWithExpectedPowerReasonHelper, CancellationToken cancellationToken);
}
