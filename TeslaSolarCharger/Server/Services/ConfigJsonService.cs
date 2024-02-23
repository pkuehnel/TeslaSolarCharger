using Microsoft.EntityFrameworkCore;
using MQTTnet.Server;
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
    private readonly IConstants _constants;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ConfigJsonService(ILogger<ConfigJsonService> logger, ISettings settings,
        IConfigurationWrapper configurationWrapper, ITeslaSolarChargerContext teslaSolarChargerContext,
        ITeslamateContext teslamateContext, IConstants constants, IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _settings = settings;
        _configurationWrapper = configurationWrapper;
        _teslaSolarChargerContext = teslaSolarChargerContext;
        _teslamateContext = teslamateContext;
        _constants = constants;
        _dateTimeProvider = dateTimeProvider;
    }

    private bool CarConfigurationFileExists()
    {
        var path = _configurationWrapper.CarConfigFileFullName();
        return File.Exists(path);
    }

    public async Task<List<DtoCar>> GetCarsFromConfiguration()
    {
        _logger.LogTrace("{method}()", nameof(GetCarsFromConfiguration));
        var cars = new List<DtoCar>();

        var carConfigurationAlreadyConverted =
            await _teslaSolarChargerContext.TscConfigurations.AnyAsync(c => c.Key == _constants.CarConfigurationsConverted).ConfigureAwait(false);

        if (!carConfigurationAlreadyConverted)
        {
            var databaseCarConfigurations = await _teslaSolarChargerContext.CachedCarStates
                .Where(c => c.Key == _constants.CarConfigurationKey)
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
                    cars.Add(new DtoCar()
                    {
                        Id = databaseCarConfiguration.CarId,
                        Vin = _teslamateContext.Cars.FirstOrDefault(c => c.Id == databaseCarConfiguration.CarId)?.Vin ?? string.Empty,
                        CarConfiguration = configuration,
                        CarState = new CarState(),
                    });
                }
            }
            await AddCachedCarStatesToCars(cars).ConfigureAwait(false);

            var tscCars = await _teslaSolarChargerContext.Cars
                .ToListAsync().ConfigureAwait(false);
            foreach (var car in cars)
            {
                var entity = tscCars.FirstOrDefault(c => c.TeslaMateCarId == car.Id) ?? new Model.Entities.TeslaSolarCharger.Car();
                entity.TeslaMateCarId = car.Id;
                entity.Name = car.CarState.Name;
                entity.Vin = car.Vin;
                entity.ChargeMode = car.CarConfiguration.ChargeMode;
                entity.MinimumSoc = car.CarConfiguration.MinimumSoC;
                entity.LatestTimeToReachSoC = car.CarConfiguration.LatestTimeToReachSoC;
                entity.IgnoreLatestTimeToReachSocDate = car.CarConfiguration.IgnoreLatestTimeToReachSocDate;
                entity.MaximumAmpere = car.CarConfiguration.MaximumAmpere;
                entity.MinimumAmpere = car.CarConfiguration.MinimumAmpere;
                entity.UsableEnergy = car.CarConfiguration.UsableEnergy;
                entity.ShouldBeManaged = car.CarConfiguration.ShouldBeManaged ?? true;
                entity.ShouldSetChargeStartTimes = car.CarConfiguration.ShouldSetChargeStartTimes ?? true;
                entity.ChargingPriority = car.CarConfiguration.ChargingPriority;
                if (entity.Id == default)
                {
                    _teslaSolarChargerContext.Cars.Add(entity);
                }
                await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
                car.Id = entity.Id;
            }

            var cachedCarStates = await _teslaSolarChargerContext.CachedCarStates.ToListAsync().ConfigureAwait(false);
            _teslaSolarChargerContext.CachedCarStates.RemoveRange(cachedCarStates);
            _teslaSolarChargerContext.TscConfigurations.Add(new TscConfiguration() { Key = _constants.CarConfigurationsConverted, Value = "true" });
            await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        }
        else
        {
            cars = await _teslaSolarChargerContext.Cars
                .Select(c => new DtoCar()
                {
                    Id = c.TeslaMateCarId,
                    Vin = c.Vin,
                    CarConfiguration = new CarConfiguration()
                    {
                        ChargeMode = c.ChargeMode,
                        MinimumSoC = c.MinimumSoc,
                        LatestTimeToReachSoC = c.LatestTimeToReachSoC,
                        IgnoreLatestTimeToReachSocDate = c.IgnoreLatestTimeToReachSocDate,
                        MaximumAmpere = c.MaximumAmpere,
                        MinimumAmpere = c.MinimumAmpere,
                        UsableEnergy = c.UsableEnergy,
                        ShouldBeManaged = c.ShouldBeManaged,
                        ShouldSetChargeStartTimes = c.ShouldSetChargeStartTimes,
                        ChargingPriority = c.ChargingPriority,
                    },
                    CarState = new CarState(),
                })
                .ToListAsync().ConfigureAwait(false);
            await AddCachedCarStatesToCars(cars).ConfigureAwait(false);
        }
        
        

        return cars;
    }

    internal List<DtoCar> DeserializeCarsFromConfigurationString(string fileContent)
    {
        _logger.LogTrace("{method}({param})", nameof(DeserializeCarsFromConfigurationString), fileContent);
        var cars = JsonConvert.DeserializeObject<List<DtoCar>>(fileContent) ?? throw new InvalidOperationException("Could not deserialize file content");
        return cars;
    }

    private async Task<string> GetCarConfigurationFileContent()
    {
        var fileContent = await File.ReadAllTextAsync(_configurationWrapper.CarConfigFileFullName()).ConfigureAwait(false);
        return fileContent;
    }

    internal void AddNewCars(List<int> newCarIds, List<DtoCar> cars)
    {
        foreach (var carId in newCarIds)
        {
            if (cars.All(c => c.Id != carId))
            {
                var car = new DtoCar
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
                .FirstOrDefaultAsync(c => c.CarId == car.Id && c.Key == _constants.CarStateKey).ConfigureAwait(false);
            if ((car.CarConfiguration.ShouldBeManaged != true) && (cachedCarState != default))
            {
                _teslaSolarChargerContext.CachedCarStates.Remove(cachedCarState);
                continue;
            }
            if (cachedCarState == null)
            {
                cachedCarState = new CachedCarState()
                {
                    CarId = car.Id,
                    Key = _constants.CarStateKey,
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
                .FirstOrDefaultAsync(c => c.CarId == car.Id && c.Key == _constants.CarConfigurationKey).ConfigureAwait(false);
            if (databaseConfig == default)
            {
                databaseConfig = new CachedCarState()
                {
                    CarId = car.Id,
                    Key = _constants.CarConfigurationKey,
                };
                _teslaSolarChargerContext.CachedCarStates.Add(databaseConfig);
            }
            databaseConfig.CarStateJson = JsonConvert.SerializeObject(car.CarConfiguration);
            databaseConfig.LastUpdated = _dateTimeProvider.UtcNow();
            var databaseCar = await _teslaSolarChargerContext.Cars.FirstOrDefaultAsync(c => c.TeslaMateCarId == car.Id).ConfigureAwait(false);
            if (databaseCar == default)
            {
                _teslaSolarChargerContext.Cars.Add(new Model.Entities.TeslaSolarCharger.Car() { TeslaMateCarId = car.Id, });
            }
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

    private async Task AddCachedCarStatesToCars(List<DtoCar> cars)
    {
        foreach (var car in cars)
        {
            var cachedCarState = await _teslaSolarChargerContext.CachedCarStates
                .FirstOrDefaultAsync(c => c.CarId == car.Id && c.Key == _constants.CarStateKey).ConfigureAwait(false);
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

    internal void RemoveOldCars(List<DtoCar> cars, List<int> stillExistingCarIds)
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
        _logger.LogTrace("{method}()", nameof(UpdateAverageGridVoltage));
        var homeGeofence = _configurationWrapper.GeoFence();
        const int lowestWorldWideGridVoltage = 100;
        const int voltageBuffer = 15;
        const int lowestGridVoltageToSearchFor = lowestWorldWideGridVoltage - voltageBuffer;
        try
        {
            var chargerVoltages = await _teslamateContext
                .Charges
                .Where(c => c.ChargingProcess.Geofence != null
                            && c.ChargingProcess.Geofence.Name == homeGeofence
                            && c.ChargerVoltage > lowestGridVoltageToSearchFor)
                .OrderByDescending(c => c.Id)
                .Select(c => c.ChargerVoltage)
                .Take(1000)
                .ToListAsync().ConfigureAwait(false);
            if (chargerVoltages.Count > 10)
            {
                var averageValue = Convert.ToInt32(chargerVoltages.Average(c => c!.Value));
                _logger.LogDebug("Use {averageVoltage}V for charge speed calculation", averageValue);
                _settings.AverageHomeGridVoltage = averageValue;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not detect average grid voltage.");
        }
    }
}
