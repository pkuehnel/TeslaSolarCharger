namespace TeslaSolarCharger.Services.Services.Contracts;

public interface ICarConfigurationService
{
    Task AddAllMissingTeslaMateCars();
}
