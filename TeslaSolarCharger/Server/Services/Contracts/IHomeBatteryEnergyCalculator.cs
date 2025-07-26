namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IHomeBatteryEnergyCalculator
{
    Task RefreshHomeBatteryMinSoc(CancellationToken cancellationToken);
}
