using SmartTeslaAmpSetter.Shared;
using SmartTeslaAmpSetter.Shared.Dtos;
using SmartTeslaAmpSetter.Shared.Enums;

namespace SmartTeslaAmpSetter.Server.Services;

public class ConfigService
{
    private readonly ILogger<ConfigService> _logger;
    private readonly Settings _settings;

    public ConfigService(ILogger<ConfigService> logger, Settings settings)
    {
        _logger = logger;
        _settings = settings;
    }

    public Settings GetSettings()
    {
        _logger.LogTrace("{method}()", nameof(GetSettings));
        return _settings;
    }

    public ChargeMode ChangeChargeMode(int carId)
    {
        var car = _settings.Cars.First(c => c.Id == carId);
        car.CarConfiguration.ChargeMode = car.CarConfiguration.ChargeMode.Next();
        return car.CarConfiguration.ChargeMode;
    }

    public void UpdateCarConfiguration(int id, CarConfiguration carConfiguration)
    {
        var existingCarIndex = _settings.Cars.FindIndex(c => c.Id == id);
        _settings.Cars[existingCarIndex].CarConfiguration = carConfiguration;
    }
}