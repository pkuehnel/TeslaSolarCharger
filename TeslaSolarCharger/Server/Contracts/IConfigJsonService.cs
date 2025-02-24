using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.CarValues;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Contracts;

public interface IConfigJsonService
{
    Task CacheCarStates();
    Task UpdateAverageGridVoltage();
    Task ConvertOldCarsToNewCar();
    Task UpdateCarBaseSettings(DtoCarBaseSettings carBaseSettings);
    Task UpdateCarBasicConfiguration(int carId, CarBasicConfiguration carBasicConfiguration);
    Task<List<CarBasicConfiguration>> GetCarBasicConfigurations();
    ISettings GetSettings();
    Task AddCarsToSettings();
    Task AddBleBaseUrlToAllCars();
    Task SetCorrectHomeDetectionVia();
}
