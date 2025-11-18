namespace TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;

public interface IRefreshableValueHandlingService
{
    Task RefreshValues();
}
