using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Contracts;

public interface IConfigJsonService
{
    Task<List<Car>> GetCarsFromConfiguration();
    Task CacheCarStates();
    Task AddCarIdsToSettings();
    Task UpdateCarConfiguration();
}
