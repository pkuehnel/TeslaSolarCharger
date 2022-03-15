using SmartTeslaAmpSetter.Shared;
using SmartTeslaAmpSetter.Shared.Dtos.Settings;
using SmartTeslaAmpSetter.Shared.Enums;

namespace SmartTeslaAmpSetter.Server.Services;

public class ConfigService
{
    private readonly ILogger<ConfigService> _logger;
    private readonly Settings _settings;
    private readonly ChargingService _chargingService;

    public ConfigService(ILogger<ConfigService> logger, Settings settings, ChargingService chargingService)
    {
        _logger = logger;
        _settings = settings;
        _chargingService = chargingService;
    }

    public async Task<Settings> GetSettings()
    {
        _logger.LogTrace("{method}()", nameof(GetSettings));
        await _chargingService.SetNewChargingValues(true);
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