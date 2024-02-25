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
        if (carConfiguration.MinimumSoC > existingCar.SocLimit)
        {
            throw new InvalidOperationException("Can not set minimum soc lower than charge limit in Tesla App");
        }
        await _configJsonService.UpdateCarConfiguration(existingCar.Vin, carConfiguration).ConfigureAwait(false);
        existingCar.ChargeMode = carConfiguration.ChargeMode;
        existingCar.MinimumSoC = carConfiguration.MinimumSoC;
        existingCar.LatestTimeToReachSoC = carConfiguration.LatestTimeToReachSoC;
        existingCar.IgnoreLatestTimeToReachSocDate = carConfiguration.IgnoreLatestTimeToReachSocDate;
        existingCar.MaximumAmpere = carConfiguration.MaximumAmpere;
        existingCar.MinimumAmpere = carConfiguration.MinimumAmpere;
        existingCar.UsableEnergy = carConfiguration.UsableEnergy;
        existingCar.ShouldBeManaged = carConfiguration.ShouldBeManaged;
        existingCar.ShouldSetChargeStartTimes = carConfiguration.ShouldSetChargeStartTimes;
        existingCar.ChargingPriority = carConfiguration.ChargingPriority;
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
        var databaseCar = _teslaSolarChargerContext.Cars.First(c => c.Id == carId);
        databaseCar.Name = carBasicConfiguration.Name;
        databaseCar.Vin = carBasicConfiguration.Vin;
        databaseCar.MinimumAmpere = carBasicConfiguration.MinimumAmpere;
        databaseCar.MaximumAmpere = carBasicConfiguration.MaximumAmpere;
        databaseCar.UsableEnergy = carBasicConfiguration.UsableEnergy;
        databaseCar.ChargingPriority = carBasicConfiguration.ChargingPriority;
        databaseCar.ShouldBeManaged = carBasicConfiguration.ShouldBeManaged;
        databaseCar.ShouldSetChargeStartTimes = carBasicConfiguration.ShouldSetChargeStartTimes;
        await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);

        var settingsCar = _settings.Cars.First(c => c.Id == carId);
        settingsCar.MinimumAmpere = carBasicConfiguration.MinimumAmpere;
        settingsCar.MaximumAmpere = carBasicConfiguration.MaximumAmpere;
        settingsCar.UsableEnergy = carBasicConfiguration.UsableEnergy;
        settingsCar.ShouldBeManaged = carBasicConfiguration.ShouldBeManaged;
        settingsCar.ChargingPriority = carBasicConfiguration.ChargingPriority;
        settingsCar.ShouldSetChargeStartTimes = carBasicConfiguration.ShouldSetChargeStartTimes;
        settingsCar.Name = carBasicConfiguration.Name;
        settingsCar.Vin = carBasicConfiguration.Vin;
    }
}
