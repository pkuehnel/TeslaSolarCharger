using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;

[assembly: InternalsVisibleTo("TeslaSolarCharger.Tests")]
namespace TeslaSolarCharger.Server.Services;

public class ConfigJsonService : IConfigJsonService
{
    private readonly ILogger<ConfigJsonService> _logger;
    private readonly ISettings _settings;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly ITeslaSolarChargerContext _teslaSolarChargerContext;
    private readonly ITeslamateContext _teslamateContext;

    public ConfigJsonService(ILogger<ConfigJsonService> logger, ISettings settings,
        IConfigurationWrapper configurationWrapper, ITeslaSolarChargerContext teslaSolarChargerContext,
        ITeslamateContext teslamateContext)
    {
        _logger = logger;
        _settings = settings;
        _configurationWrapper = configurationWrapper;
        _teslaSolarChargerContext = teslaSolarChargerContext;
        _teslamateContext = teslamateContext;
    }

    private bool CarConfigurationFileExists()
    {
        var path = _configurationWrapper.CarConfigFileFullName();
        return File.Exists(path);
    }

    public async Task<List<Car>> GetCarsFromConfiguration()
    {
        var cars = new List<Car>();
        if (CarConfigurationFileExists())
        {
            try
            {
                var fileContent = await GetCarConfigurationFileContent().ConfigureAwait(false);
                cars = DeserializeCarsFromConfigurationString(fileContent);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get car configurations, use default configuration");
            }
        }

        try
        {
            var carIds = await _teslamateContext.Cars.Select(c => (int)c.Id).ToListAsync().ConfigureAwait(false);
            RemoveOldCars(cars, carIds);

            var newCarIds = carIds.Where(i => !cars.Any(c => c.Id == i)).ToList();
            await AddNewCars(newCarIds, cars).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not get cars from TeslaMate database.");
        }
        return cars;
    }

    internal List<Car> DeserializeCarsFromConfigurationString(string fileContent)
    {
        _logger.LogTrace("{method}({param})", nameof(DeserializeCarsFromConfigurationString), fileContent);
        var cars = JsonConvert.DeserializeObject<List<Car>>(fileContent) ?? throw new InvalidOperationException("Could not deserialize file content");
        foreach (var car in cars)
        {
            car.CarState.ShouldStopChargingSince = null;
            car.CarState.ShouldStartChargingSince = null;
        }


        return cars;
    }

    private async Task<string> GetCarConfigurationFileContent()
    {
        var fileContent = await File.ReadAllTextAsync(_configurationWrapper.CarConfigFileFullName()).ConfigureAwait(false);
        return fileContent;
    }

    internal async Task AddNewCars(List<int> newCarIds, List<Car> cars)
    {
        foreach (var carId in newCarIds)
        {
            if (cars.All(c => c.Id != carId))
            {
                var car = new Car
                {
                    Id = carId,
                    CarConfiguration =
                    {
                        ChargeMode = ChargeMode.PvAndMinSoc,
                        MaximumAmpere = 16,
                        MinimumAmpere = 1,
                        UsableEnergy = 75,
                        LatestTimeToReachSoC = new DateTime(2022, 1, 1),
                        ShouldBeManaged = true,
                    },
                    CarState =
                    {
                        ShouldStartChargingSince = null,
                        ShouldStopChargingSince = null,
                    },
                };
                cars.Add(car);
            }
        }

        await UpdateCarConfiguration().ConfigureAwait(false);
    }

    public async Task CacheCarStates()
    {
        _logger.LogTrace("{method}()", nameof(CacheCarStates));
        foreach (var car in _settings.Cars)
        {
            var cachedCarState = await _teslaSolarChargerContext.CachedCarStates
                .FirstOrDefaultAsync(c => c.CarId == car.Id).ConfigureAwait(false);
            if (cachedCarState == null)
            {
                cachedCarState = new CachedCarState()
                {
                    CarId = car.Id,
                };
                _teslaSolarChargerContext.CachedCarStates.Add(cachedCarState);
            }

            if (car.CarState.SocLimit != default)
            {
                cachedCarState.CarStateJson = JsonConvert.SerializeObject(car.CarState);
                cachedCarState.LastUpdated = DateTime.UtcNow;
                await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task UpdateCarConfiguration()
    {
        _logger.LogTrace("{method}()", nameof(UpdateCarConfiguration));
        var configFileLocation = _configurationWrapper.CarConfigFileFullName();

        _logger.LogDebug("Update configuration.json");
        var fileInfo = new FileInfo(configFileLocation);
        var configDirectoryFullName = fileInfo.Directory?.FullName;
        if (!Directory.Exists(configDirectoryFullName))
        {
            _logger.LogDebug("Config directory {directoryname} does not exist.", configDirectoryFullName);
            Directory.CreateDirectory(configDirectoryFullName ?? throw new InvalidOperationException());
        }

        var settings = new JsonSerializerSettings()
        {
            ContractResolver = new ConfigPropertyResolver()
        };
        _logger.LogDebug("Using {@cars} to create new json file", _settings.Cars);
        var json = JsonConvert.SerializeObject(_settings.Cars, settings);
        _logger.LogDebug("Created json to save as config file: {json}", json);
        await File.WriteAllTextAsync(configFileLocation, json).ConfigureAwait(false);
    }

    public async Task AddCarIdsToSettings()
    {
        _logger.LogTrace("{method}", nameof(AddCarIdsToSettings));
        _settings.Cars = await GetCarsFromConfiguration().ConfigureAwait(false);
        _logger.LogDebug("All cars added to settings");
        foreach (var car in _settings.Cars)
        {
            if (car.CarConfiguration.UsableEnergy < 1)
            {
                car.CarConfiguration.UsableEnergy = 75;
            }

            if (car.CarConfiguration.MaximumAmpere < 1)
            {
                car.CarConfiguration.MaximumAmpere = 16;
            }

            if (car.CarConfiguration.MinimumAmpere < 1)
            {
                car.CarConfiguration.MinimumAmpere = 1;
            }

            if (car.CarConfiguration.ChargingPriority < 1)
            {
                car.CarConfiguration.ChargingPriority = 1;
            }

            if (car.CarConfiguration.ShouldBeManaged == null)
            {
                var defaultValue = true;
                _logger.LogInformation("Car {carId}: {variable} is not set, use default value {defaultValue}", car.Id, nameof(car.CarConfiguration.ShouldBeManaged), defaultValue);
                car.CarConfiguration.ShouldBeManaged = defaultValue;
            }
        }

        await UpdateCarConfiguration().ConfigureAwait(false);
        _logger.LogDebug("All unset car configurations set.");
    }

    internal void RemoveOldCars(List<Car> cars, List<int> stillExistingCarIds)
    {
        var carsIdsToRemove = cars
            .Where(c => !stillExistingCarIds.Any(i => c.Id == i))
            .Select(c => c.Id)
            .ToList();
        foreach (var carId in carsIdsToRemove)
        {
            cars.RemoveAll(c => c.Id == carId);
        }
    }
}
