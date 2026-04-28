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
    Task UpdateCarBasicConfiguration(int carId, CarBasicConfiguration carBasicConfiguration);
    Task<List<CarBasicConfiguration>> GetCarBasicConfigurations(int? carId = null);
    ISettings GetSettings();
    Task AddCarsToSettings();
    Task AddBleBaseUrlToAllCars();
    Task SetCorrectHomeDetectionVia();
    Task AddAllTeslasToAllowedCars();
    Task DisconnectCarFromStartCar(int carId);
}
