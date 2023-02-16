using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Contracts;

public interface IConfigService
{
    ISettings GetSettings();
    Task UpdateCarConfiguration(int carId, CarConfiguration carConfiguration);
    Task<List<CarBasicConfiguration>> GetCarBasicConfigurations();
    Task UpdateCarBasicConfiguration(int carId, CarBasicConfiguration carBasicConfiguration);
}
