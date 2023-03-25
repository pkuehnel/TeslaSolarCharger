using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedBackend.Contracts;

[assembly: InternalsVisibleTo("TeslaSolarCharger.Tests")]
namespace TeslaSolarCharger.Server.Services;

public class ConfigJsonService : IConfigJsonService
{
    private readonly ILogger<ConfigJsonService> _logger;
    private readonly ISettings _settings;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly ITeslaSolarChargerContext _teslaSolarChargerContext;
    private readonly ITeslamateContext _teslamateContext;
    private readonly IContstants _contstants;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ConfigJsonService(ILogger<ConfigJsonService> logger, ISettings settings,
        IConfigurationWrapper configurationWrapper, ITeslaSolarChargerContext teslaSolarChargerContext,
        ITeslamateContext teslamateContext, IContstants contstants, IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _settings = settings;
        _configurationWrapper = configurationWrapper;
        _teslaSolarChargerContext = teslaSolarChargerContext;
        _teslamateContext = teslamateContext;
        _contstants = contstants;
        _dateTimeProvider = dateTimeProvider;
    }

    private bool CarConfigurationFileExists()
    {
        var path = _configurationWrapper.CarConfigFileFullName();
        return File.Exists(path);
    }

    public async Task<List<Car>> GetCarsFromConfiguration()
    {
        var cars = new List<Car>();
        var databaseCarConfigurations = await _teslaSolarChargerContext.CachedCarStates
            .Where(c => c.Key == _contstants.CarConfigurationKey)
            .ToListAsync().ConfigureAwait(false);
        if (databaseCarConfigurations.Count < 1 && CarConfigurationFileExists())
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

        if (databaseCarConfigurations.Count > 0)
        {
            foreach (var databaseCarConfiguration in databaseCarConfigurations)
            {
                var configuration = JsonConvert.DeserializeObject<CarConfiguration>(databaseCarConfiguration.CarStateJson ?? string.Empty);
                if (configuration == default)
                {
                    continue;
                }
                cars.Add(new Car()
                {
                    Id = databaseCarConfiguration.CarId,
                    CarConfiguration = configuration,
                    CarState = new CarState(),
                });
            }
        }

        try
        {
            var carIds = await _teslamateContext.Cars.Select(c => (int)c.Id).ToListAsync().ConfigureAwait(false);
            RemoveOldCars(cars, carIds);

            var newCarIds = carIds.Where(i => !cars.Any(c => c.Id == i)).ToList();
            AddNewCars(newCarIds, cars);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not get cars from TeslaMate database.");
        }
        await AddCachedCarStatesToCars(cars).ConfigureAwait(false);

        return cars;
    }

    internal List<Car> DeserializeCarsFromConfigurationString(string fileContent)
    {
        _logger.LogTrace("{method}({param})", nameof(DeserializeCarsFromConfigurationString), fileContent);
        var cars = JsonConvert.DeserializeObject<List<Car>>(fileContent) ?? throw new InvalidOperationException("Could not deserialize file content");
        return cars;
    }

    private async Task<string> GetCarConfigurationFileContent()
    {
        var fileContent = await File.ReadAllTextAsync(_configurationWrapper.CarConfigFileFullName()).ConfigureAwait(false);
        return fileContent;
    }

    internal void AddNewCars(List<int> newCarIds, List<Car> cars)
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
    }

    public async Task CacheCarStates()
    {
        _logger.LogTrace("{method}()", nameof(CacheCarStates));
        foreach (var car in _settings.Cars)
        {
            var cachedCarState = await _teslaSolarChargerContext.CachedCarStates
                .FirstOrDefaultAsync(c => c.CarId == car.Id && c.Key == _contstants.CarStateKey).ConfigureAwait(false);
            if (cachedCarState == null)
            {
                cachedCarState = new CachedCarState()
                {
                    CarId = car.Id,
                    Key = _contstants.CarStateKey,
                };
                _teslaSolarChargerContext.CachedCarStates.Add(cachedCarState);
            }

            if (car.CarState.SocLimit != default)
            {
                cachedCarState.CarStateJson = JsonConvert.SerializeObject(car.CarState);
                cachedCarState.LastUpdated = _dateTimeProvider.UtcNow();
            }
        }
        await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task UpdateCarConfiguration()
    {
        _logger.LogTrace("{method}()", nameof(UpdateCarConfiguration));
        foreach (var car in _settings.Cars)
        {
            var databaseConfig = await _teslaSolarChargerContext.CachedCarStates
                .FirstOrDefaultAsync(c => c.CarId == car.Id && c.Key == _contstants.CarConfigurationKey).ConfigureAwait(false);
            if (databaseConfig == default)
            {
                databaseConfig = new CachedCarState()
                {
                    CarId = car.Id,
                    Key = _contstants.CarConfigurationKey,
                };
                _teslaSolarChargerContext.CachedCarStates.Add(databaseConfig);
            }
            databaseConfig.CarStateJson = JsonConvert.SerializeObject(car.CarConfiguration);
            databaseConfig.LastUpdated = _dateTimeProvider.UtcNow();
        }
        await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
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

    private async Task AddCachedCarStatesToCars(List<Car> cars)
    {
        foreach (var car in cars)
        {
            var cachedCarState = await _teslaSolarChargerContext.CachedCarStates
                .FirstOrDefaultAsync(c => c.CarId == car.Id && c.Key == _contstants.CarStateKey).ConfigureAwait(false);
            if (cachedCarState == default)
            {
                _logger.LogWarning("No cached car state found for car with id {carId}", car.Id);
                continue;
            }

            var carState = JsonConvert.DeserializeObject<CarState>(cachedCarState.CarStateJson ?? string.Empty);
            if (carState == null)
            {
                _logger.LogWarning("Could not deserialized cached car state for car with id {carId}", car.Id);
                continue;
            }

            car.CarState = carState;
        }
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

    [SuppressMessage("ReSharper.DPA", "DPA0007: Large number of DB records", MessageId = "count: 1000")]
    public async Task UpdateAverageGridVoltage()
    {
        var homeGeofence = _configurationWrapper.GeoFence();
        var lowestWorldWideGridVoltage = 100;
        var voltageBuffer = 15;
        var lowestGridVoltageToSearchFor = lowestWorldWideGridVoltage - voltageBuffer;
        var chargerVoltages = await _teslamateContext
            .Charges
            .Where(c => c.ChargingProcess.Geofence != null
                        && c.ChargingProcess.Geofence.Name == homeGeofence
                        && c.ChargerVoltage > lowestGridVoltageToSearchFor)
            .Select(c => c.ChargerVoltage)
            .Take(1000)
            .ToListAsync().ConfigureAwait(false);
        if (chargerVoltages.Count > 10)
        {
            var averageValue = Convert.ToInt32(chargerVoltages.Average(c => c!.Value));
            _logger.LogDebug("Use {averageVoltage}V for charge speed calculation", averageValue);
            _settings.AverageGridHomeGridVoltage = averageValue;
        }

    }
}
