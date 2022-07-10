using SolarTeslaCharger.Shared.Dtos;
using SolarTeslaCharger.Shared.Dtos.Contracts;
using SolarTeslaCharger.Shared.Dtos.Settings;
using SolarTeslaCharger.Shared.Enums;

namespace SolarTeslaCharger.Server.Contracts;

public interface IConfigService
{
    ISettings GetSettings();
    ChargeMode ChangeChargeMode(int carId);
    void UpdateCarConfiguration(int carId, CarConfiguration carConfiguration);
    List<CarBasicConfiguration> GetCarBasicConfigurations();
    void UpdateCarBasicConfiguration(int carId, CarBasicConfiguration carBasicConfiguration);
}