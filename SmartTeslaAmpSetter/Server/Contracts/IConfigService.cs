using SmartTeslaAmpSetter.Shared.Dtos;
using SmartTeslaAmpSetter.Shared.Dtos.Settings;
using SmartTeslaAmpSetter.Shared.Enums;

namespace SmartTeslaAmpSetter.Server.Contracts;

public interface IConfigService
{
    Task<Settings> GetSettings();
    ChargeMode ChangeChargeMode(int carId);
    void UpdateCarConfiguration(int id, CarConfiguration carConfiguration);
    List<CarBasicConfiguration> GetCarBasicConfigurations();
    void UpdateCarBasicConfiguration(int carId, CarBasicConfiguration carBasicConfiguration);
}