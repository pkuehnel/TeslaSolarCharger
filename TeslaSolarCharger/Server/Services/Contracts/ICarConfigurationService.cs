namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ICarConfigurationService
{
    /// <summary>
    /// Adds all cars of the connected Tesla account that are not known to TSC yet.
    /// </summary>
    /// <returns>The number of cars that were newly added.</returns>
    Task<int> AddAllMissingCarsFromTeslaAccount();
}
