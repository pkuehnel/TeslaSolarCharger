using SolarTeslaCharger.Shared.Dtos.Settings;

namespace SolarTeslaCharger.Server.Contracts;

public interface IConfigJsonService
{
    Task<List<Car>> GetCarsFromConfiguration();
    Task UpdateConfigJson();
    Task AddCarIdsToSettings();
}