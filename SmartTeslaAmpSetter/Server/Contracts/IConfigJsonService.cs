using SmartTeslaAmpSetter.Shared.Dtos.Settings;

namespace SmartTeslaAmpSetter.Server.Contracts;

public interface IConfigJsonService
{
    Task<List<Car>> GetCarsFromConfiguration();
    Task UpdateConfigJson();
}