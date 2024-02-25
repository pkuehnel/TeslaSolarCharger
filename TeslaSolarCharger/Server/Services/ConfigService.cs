using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.MappingExtensions;
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
    private readonly IMapperConfigurationFactory _mapperConfigurationFactory;
    private readonly ITeslaSolarChargerContext _teslaSolarChargerContext;

    public ConfigService(ILogger<ConfigService> logger,
        ISettings settings, IIndexService indexService, IConfigJsonService configJsonService,
        IMapperConfigurationFactory mapperConfigurationFactory,
        ITeslaSolarChargerContext teslaSolarChargerContext)
    {
        _logger = logger;
        _settings = settings;
        _indexService = indexService;
        _configJsonService = configJsonService;
        _mapperConfigurationFactory = mapperConfigurationFactory;
        _teslaSolarChargerContext = teslaSolarChargerContext;
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
        await _configJsonService.UpdateCarConfiguration(existingCar.Vin, existingCar.CarConfiguration).ConfigureAwait(false);
    }

    public async Task<List<CarBasicConfiguration>> GetCarBasicConfigurations()
    {
        _logger.LogTrace("{method}()", nameof(GetCarBasicConfigurations));

        var mapper = _mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<Car, CarBasicConfiguration>()
                ;
        });

        var cars = await _teslaSolarChargerContext.Cars
            .ProjectTo<CarBasicConfiguration>(mapper)
            .ToListAsync().ConfigureAwait(false);

        return cars;
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
        car.CarState.Name = carBasicConfiguration.Name;
        car.Vin = carBasicConfiguration.Vin;
        await _configJsonService.UpdateCarConfiguration(car.Vin, car.CarConfiguration).ConfigureAwait(false);
    }
}
