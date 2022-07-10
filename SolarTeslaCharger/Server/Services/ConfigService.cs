using SolarTeslaCharger.Server.Contracts;
using SolarTeslaCharger.Shared;
using SolarTeslaCharger.Shared.Dtos;
using SolarTeslaCharger.Shared.Dtos.Contracts;
using SolarTeslaCharger.Shared.Dtos.Settings;
using SolarTeslaCharger.Shared.Enums;

namespace SolarTeslaCharger.Server.Services;

public class ConfigService : IConfigService
{
    private readonly ILogger<ConfigService> _logger;
    private readonly ISettings _settings;

    public ConfigService(ILogger<ConfigService> logger, ISettings settings)
    {
        _logger = logger;
        _settings = settings;
    }

    public ISettings GetSettings()
    {
        _logger.LogTrace("{method}()", nameof(GetSettings));
        return _settings;
    }

    public ChargeMode ChangeChargeMode(int carId)
    {
        _logger.LogTrace("{method},({param1})", nameof(ChangeChargeMode), carId);
        var car = _settings.Cars.First(c => c.Id == carId);
        car.CarConfiguration.ChargeMode = car.CarConfiguration.ChargeMode.Next();
        car.CarState.AutoFullSpeedCharge = false;
        return car.CarConfiguration.ChargeMode;
    }

    public void UpdateCarConfiguration(int carId, CarConfiguration carConfiguration)
    {
        _logger.LogTrace("{method}({param1}, {@param2})", nameof(UpdateCarConfiguration), carId, carConfiguration);
        var existingCarIndex = _settings.Cars.FindIndex(c => c.Id == carId);
        _settings.Cars[existingCarIndex].CarConfiguration = carConfiguration;
    }

    public List<CarBasicConfiguration> GetCarBasicConfigurations()
    {
        _logger.LogTrace("{method}()", nameof(GetCarBasicConfigurations));
        var carSettings = new List<CarBasicConfiguration>();

        foreach (var car in _settings.Cars)
        {
            carSettings.Add(new CarBasicConfiguration(car.Id, car.CarState.Name)
            {
                MaximumAmpere = car.CarConfiguration.MaximumAmpere,
                MinimumAmpere = car.CarConfiguration.MinimumAmpere,
                UsableEnergy = car.CarConfiguration.UsableEnergy,
                ShouldBeManaged = car.CarConfiguration.ShouldBeManaged,
            });
        }

        return carSettings;
    }

    public void UpdateCarBasicConfiguration(int carId, CarBasicConfiguration carBasicConfiguration)
    {
        _logger.LogTrace("{method}({param1}, {@param2})", nameof(UpdateCarBasicConfiguration), carId, carBasicConfiguration);
        var car = _settings.Cars.First(c => c.Id == carId);
        car.CarConfiguration.MinimumAmpere = carBasicConfiguration.MinimumAmpere;
        car.CarConfiguration.MaximumAmpere = carBasicConfiguration.MaximumAmpere;
        car.CarConfiguration.UsableEnergy = carBasicConfiguration.UsableEnergy;
        car.CarConfiguration.ShouldBeManaged = carBasicConfiguration.ShouldBeManaged;
    }
}