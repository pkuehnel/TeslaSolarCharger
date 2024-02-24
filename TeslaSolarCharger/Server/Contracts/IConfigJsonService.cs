using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Contracts;

public interface IConfigJsonService
{
    Task CacheCarStates();
    Task AddCarIdsToSettings();
    Task UpdateAverageGridVoltage();
    Task UpdateCarConfiguration(string carVin, CarConfiguration carConfiguration);
}
