namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ICarConfigurationService
{
    Task AddAllMissingCarsFromTeslaAccount();
}
