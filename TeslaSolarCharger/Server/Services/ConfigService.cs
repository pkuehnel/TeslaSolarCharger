using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Server.Services;

public class ConfigService : IConfigService
{
    private readonly ILogger<ConfigService> _logger;
    private readonly ISettings _settings;
    private readonly IIndexService _indexService;
    private readonly IConfigJsonService _configJsonService;

    public ConfigService(ILogger<ConfigService> logger, ISettings settings, IIndexService indexService, IConfigJsonService configJsonService)
    {
        _logger = logger;
        _settings = settings;
        _indexService = indexService;
        _configJsonService = configJsonService;
    }

    public ISettings GetSettings()
    {
        _logger.LogTrace("{method}()", nameof(GetSettings));
        return _settings;
    }

    public async Task UpdateCarConfiguration(int carId, CarConfiguration carConfiguration)
    {
        _logger.LogTrace("{method}({param1}, {@param2})", nameof(UpdateCarConfiguration), carId, carConfiguration);
        var existingCar = _settings.Cars.First(c => c.Id == carId);
        if (carConfiguration.MinimumSoC > existingCar.CarState.SocLimit)
        {
            throw new InvalidOperationException("Can not set minimum soc lower than charge limit in Tesla App");
        }
        existingCar.CarConfiguration = carConfiguration;
        await _configJsonService.UpdateCarConfiguration().ConfigureAwait(false);
    }

    public async Task<List<CarBasicConfiguration>> GetCarBasicConfigurations()
    {
        _logger.LogTrace("{method}()", nameof(GetCarBasicConfigurations));
        var carSettings = new List<CarBasicConfiguration>();
        foreach (var car in _settings.Cars)
        {
            var carBasicConfiguration = new CarBasicConfiguration(car.Id, car.CarState.Name)
            {
                MaximumAmpere = car.CarConfiguration.MaximumAmpere,
                MinimumAmpere = car.CarConfiguration.MinimumAmpere,
                UsableEnergy = car.CarConfiguration.UsableEnergy,
                ShouldBeManaged = car.CarConfiguration.ShouldBeManaged,
                ChargingPriority = car.CarConfiguration.ChargingPriority,
                ShouldSetChargeStartTimes = car.CarConfiguration.ShouldSetChargeStartTimes == true,
            };
            try
            {
                carBasicConfiguration.VehicleIdentificationNumber =
                    await _indexService.GetVinByCarId(car.Id).ConfigureAwait(false) ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not get VIN of car {carId}", car.Id);
            }

            carSettings.Add(carBasicConfiguration);
        }

        return carSettings.OrderBy(c => c.CarId).ToList();
    }

    public async Task UpdateCarBasicConfiguration(int carId, CarBasicConfiguration carBasicConfiguration)
    {
        _logger.LogTrace("{method}({param1}, {@param2})", nameof(UpdateCarBasicConfiguration), carId, carBasicConfiguration);
        var car = _settings.Cars.First(c => c.Id == carId);
        car.CarConfiguration.MinimumAmpere = carBasicConfiguration.MinimumAmpere;
        car.CarConfiguration.MaximumAmpere = carBasicConfiguration.MaximumAmpere;
        car.CarConfiguration.UsableEnergy = carBasicConfiguration.UsableEnergy;
        car.CarConfiguration.ShouldBeManaged = carBasicConfiguration.ShouldBeManaged;
        car.CarConfiguration.ChargingPriority = carBasicConfiguration.ChargingPriority;
        car.CarConfiguration.ShouldSetChargeStartTimes = carBasicConfiguration.ShouldSetChargeStartTimes;
        await _configJsonService.UpdateCarConfiguration().ConfigureAwait(false);
    }
}
