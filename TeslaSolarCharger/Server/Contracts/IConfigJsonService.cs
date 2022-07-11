using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Contracts;

public interface IConfigJsonService
{
    Task<List<Car>> GetCarsFromConfiguration();
    Task UpdateConfigJson();
    Task AddCarIdsToSettings();
}