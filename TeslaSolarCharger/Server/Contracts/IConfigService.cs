using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Contracts;

public interface IConfigService
{
    ISettings GetSettings();
    ChargeMode ChangeChargeMode(int carId);
    void UpdateCarConfiguration(int carId, CarConfiguration carConfiguration);
    Task<List<CarBasicConfiguration>> GetCarBasicConfigurations();
    void UpdateCarBasicConfiguration(int carId, CarBasicConfiguration carBasicConfiguration);
}
