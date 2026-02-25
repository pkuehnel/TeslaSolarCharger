namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ICarDataProviderOrchestrator
{
    Task RefreshAllCarData();
}
