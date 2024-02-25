using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.CarValues;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Contracts;

public interface IConfigJsonService
{
    Task CacheCarStates();
    Task UpdateAverageGridVoltage();
    Task SaveOrUpdateCar(DtoCar car);
    Task<List<DtoCar>> GetCars();
    Task<List<DtoCar>> GetCarById(int id);
    Task ConvertOldCarsToNewCar();
    Task UpdateCarBaseSettings(DtoCarBaseSettings carBaseSettings);
    Task UpdateCarBasicConfiguration(int carId, CarBasicConfiguration carBasicConfiguration);
    Task<List<CarBasicConfiguration>> GetCarBasicConfigurations();
    ISettings GetSettings();
    Task AddCarsToSettings();
}
