namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IPowerToControlCalculationService
{
    Task<int> CalculatePowerToControl(int currentChargingPower, CancellationToken cancellationToken);
}
