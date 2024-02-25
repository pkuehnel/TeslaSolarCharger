using TeslaSolarCharger.Shared.Dtos.IndexRazor.CarValues;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Contracts;

public interface IConfigJsonService
{
    Task CacheCarStates();
    Task UpdateAverageGridVoltage();
    Task UpdateCarConfiguration(string carVin, CarConfiguration carConfiguration);
    Task SaveOrUpdateCar(DtoCar car);
    Task<List<DtoCar>> GetCars();
    Task<List<DtoCar>> GetCarById(int id);
    Task ConvertOldCarsToNewCar();
    Task UpdateCarBaseSettings(DtoCarBaseSettings carBaseSettings);
}
