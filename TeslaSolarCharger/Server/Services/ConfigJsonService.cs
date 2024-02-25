using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.MappingExtensions;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedBackend.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.CarValues;

[assembly: InternalsVisibleTo("TeslaSolarCharger.Tests")]
namespace TeslaSolarCharger.Server.Services;

public class ConfigJsonService(
    ILogger<ConfigJsonService> logger,
    ISettings settings,
    IConfigurationWrapper configurationWrapper,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    ITeslamateContext teslamateContext,
    IConstants constants,
    IDateTimeProvider dateTimeProvider,
    IMapperConfigurationFactory mapperConfigurationFactory)
    : IConfigJsonService
{
    private bool CarConfigurationFileExists()
    {
        var path = configurationWrapper.CarConfigFileFullName();
        return File.Exists(path);
    }

    public async Task ConvertOldCarsToNewCar()
    {
        logger.LogTrace("{method}()", nameof(ConvertOldCarsToNewCar));
        var cars = new List<DtoCar>();

        var carConfigurationAlreadyConverted =
            await teslaSolarChargerContext.TscConfigurations.AnyAsync(c => c.Key == constants.CarConfigurationsConverted).ConfigureAwait(false);

        if (carConfigurationAlreadyConverted)
        {
            return;
        }
        var oldCarConfiguration = await teslaSolarChargerContext.CachedCarStates
            .Where(c => c.Key == constants.CarConfigurationKey)
            .ToListAsync().ConfigureAwait(false);
        if (oldCarConfiguration.Count < 1 && CarConfigurationFileExists())
        {
            try
            {
                var fileContent = await GetCarConfigurationFileContent().ConfigureAwait(false);
                cars = DeserializeCarsFromConfigurationString(fileContent);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not get car configurations, use default configuration");
            }
        }

        if (oldCarConfiguration.Count > 0)
        {
            foreach (var databaseCarConfiguration in oldCarConfiguration)
            {
                var configuration =
                    JsonConvert.DeserializeObject<DepricatedCarConfiguration>(databaseCarConfiguration.CarStateJson ?? string.Empty);
                if (configuration == default)
                {
                    continue;
                }

                cars.Add(new DtoCar()
                {
                    Vin = (await teslamateContext.Cars.FirstOrDefaultAsync(c => c.Id == databaseCarConfiguration.CarId).ConfigureAwait(false))?.Vin ?? string.Empty,
                    ChargeMode = configuration.ChargeMode,
                    MinimumSoC = configuration.MinimumSoC,
                    LatestTimeToReachSoC = configuration.LatestTimeToReachSoC,
                    IgnoreLatestTimeToReachSocDate = configuration.IgnoreLatestTimeToReachSocDate,
                    MaximumAmpere = configuration.MaximumAmpere,
                    MinimumAmpere = configuration.MinimumAmpere,
                    UsableEnergy = configuration.UsableEnergy,
                    ShouldBeManaged = configuration.ShouldBeManaged,
                    ShouldSetChargeStartTimes = configuration.ShouldSetChargeStartTimes,
                    ChargingPriority = configuration.ChargingPriority,
                });
            }

            await AddCachedCarStatesToCars(cars).ConfigureAwait(false);
            foreach (var car in cars)
            {
                await SaveOrUpdateCar(car).ConfigureAwait(false);
            }

            var cachedCarStates = await teslaSolarChargerContext.CachedCarStates.ToListAsync().ConfigureAwait(false);
            teslaSolarChargerContext.CachedCarStates.RemoveRange(cachedCarStates);
            teslaSolarChargerContext.TscConfigurations.Add(new TscConfiguration()
            {
                Key = constants.CarConfigurationsConverted,
                Value = "true",
            });
            await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
            throw new NotImplementedException(
                "For each car with a different TeslaMateCarId than TSC car ID all HandledCharges' CarIds need to be updated");
        }
    }

    public async Task UpdateCarBaseSettings(DtoCarBaseSettings carBaseSettings)
    {
        logger.LogTrace("{method}({@carBaseSettings})", nameof(UpdateCarBaseSettings), carBaseSettings);
        var car = await teslaSolarChargerContext.Cars.FirstAsync(c => c.Id == carBaseSettings.CarId).ConfigureAwait(false);
        car.ChargeMode = carBaseSettings.ChargeMode;
        car.MinimumSoc = carBaseSettings.MinimumStateOfCharge;
        car.LatestTimeToReachSoC = carBaseSettings.LatestTimeToReachStateOfCharge;
        car.IgnoreLatestTimeToReachSocDate = carBaseSettings.IgnoreLatestTimeToReachSocDate;
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);

    }

    public async Task UpdateCarBasicConfiguration(int carId, CarBasicConfiguration carBasicConfiguration)
    {
        logger.LogTrace("{method}({carId}, {@carBasicConfiguration})", nameof(UpdateCarBasicConfiguration), carId, carBasicConfiguration);
        var databaseCar = teslaSolarChargerContext.Cars.First(c => c.Id == carId);
        databaseCar.Name = carBasicConfiguration.Name;
        databaseCar.Vin = carBasicConfiguration.Vin;
        databaseCar.MinimumAmpere = carBasicConfiguration.MinimumAmpere;
        databaseCar.MaximumAmpere = carBasicConfiguration.MaximumAmpere;
        databaseCar.UsableEnergy = carBasicConfiguration.UsableEnergy;
        databaseCar.ChargingPriority = carBasicConfiguration.ChargingPriority;
        databaseCar.ShouldBeManaged = carBasicConfiguration.ShouldBeManaged;
        databaseCar.ShouldSetChargeStartTimes = carBasicConfiguration.ShouldSetChargeStartTimes;
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        var settingsCar = settings.Cars.First(c => c.Id == carId);
        settingsCar.Name = carBasicConfiguration.Name;
        settingsCar.Vin = carBasicConfiguration.Vin;
        settingsCar.MinimumAmpere = carBasicConfiguration.MinimumAmpere;
        settingsCar.MaximumAmpere = carBasicConfiguration.MaximumAmpere;
        settingsCar.UsableEnergy = carBasicConfiguration.UsableEnergy;
        settingsCar.ChargingPriority = carBasicConfiguration.ChargingPriority;
        settingsCar.ShouldBeManaged = carBasicConfiguration.ShouldBeManaged;
        settingsCar.ShouldSetChargeStartTimes = carBasicConfiguration.ShouldSetChargeStartTimes;
    }

    public Task UpdateCarConfiguration(int carId, DepricatedCarConfiguration carConfiguration)
    {
        throw new NotImplementedException();
    }

    public async Task SaveOrUpdateCar(DtoCar car)
    {
        var entity = teslaSolarChargerContext.Cars.FirstOrDefault(c => c.TeslaMateCarId == car.Id) ?? new Car()
        {
            Id = car.Id,
            TeslaMateCarId = teslamateContext.Cars.FirstOrDefault(c => c.Vin == car.Vin)?.Id ?? default,
        };
        entity.Name = car.Name;
        entity.Vin = car.Vin;
        entity.ChargeMode = car.ChargeMode;
        entity.MinimumSoc = car.MinimumSoC;
        entity.LatestTimeToReachSoC = car.LatestTimeToReachSoC;
        entity.IgnoreLatestTimeToReachSocDate = car.IgnoreLatestTimeToReachSocDate;
        entity.MaximumAmpere = car.MaximumAmpere;
        entity.MinimumAmpere = car.MinimumAmpere;
        entity.UsableEnergy = car.UsableEnergy;
        entity.ShouldBeManaged = car.ShouldBeManaged ?? true;
        entity.ShouldSetChargeStartTimes = car.ShouldSetChargeStartTimes ?? true;
        entity.ChargingPriority = car.ChargingPriority;
        entity.SoC = car.SoC;
        entity.SocLimit = car.SocLimit;
        entity.ChargerPhases = car.ChargerPhases;
        entity.ChargerVoltage = car.ChargerVoltage;
        entity.ChargerActualCurrent = car.ChargerActualCurrent;
        entity.ChargerPilotCurrent = car.ChargerPilotCurrent;
        entity.ChargerRequestedCurrent = car.ChargerRequestedCurrent;
        entity.PluggedIn = car.PluggedIn;
        entity.ClimateOn = car.ClimateOn;
        entity.Latitude = car.Latitude;
        entity.Longitude = car.Longitude;
        entity.State = car.State;
        if (entity.Id == default)
        {
            teslaSolarChargerContext.Cars.Add(entity);
        }
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<List<DtoCar>> GetCarById(int id)
    {
        logger.LogTrace("{method}({id})", nameof(GetCarById), id);
        var mapper = mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<Car, DtoCar>()
                ;
        });
        var cars = await teslaSolarChargerContext.Cars
            .ProjectTo<DtoCar>(mapper)
            .ToListAsync().ConfigureAwait(false);
        return cars;
    }

    public async Task<List<DtoCar>> GetCars()
    {
        logger.LogTrace("{method}()", nameof(GetCars));
        var mapper = mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<Car, DtoCar>()
                ;
        });
        var cars = await teslaSolarChargerContext.Cars
            .ProjectTo<DtoCar>(mapper)
            .ToListAsync().ConfigureAwait(false);
        return cars;
    }

    internal List<DtoCar> DeserializeCarsFromConfigurationString(string fileContent)
    {
        logger.LogTrace("{method}({param})", nameof(DeserializeCarsFromConfigurationString), fileContent);
        var cars = JsonConvert.DeserializeObject<List<DtoCar>>(fileContent) ?? throw new InvalidOperationException("Could not deserialize file content");
        return cars;
    }

    private async Task<string> GetCarConfigurationFileContent()
    {
        var fileContent = await File.ReadAllTextAsync(configurationWrapper.CarConfigFileFullName()).ConfigureAwait(false);
        return fileContent;
    }

    public async Task CacheCarStates()
    {
        logger.LogTrace("{method}()", nameof(CacheCarStates));
        foreach (var car in settings.Cars)
        {
            var dbCar = await teslaSolarChargerContext.Cars.FirstOrDefaultAsync(c => c.Id == car.Id).ConfigureAwait(false);
            if (dbCar == default)
            {
                logger.LogWarning("Car with id {carId} not found in database", car.Id);
                continue;
            }
            dbCar.SoC = car.SoC;
            dbCar.SocLimit = car.SocLimit;
            dbCar.ChargerPhases = car.ChargerPhases;
            dbCar.ChargerVoltage = car.ChargerVoltage;
            dbCar.ChargerActualCurrent = car.ChargerActualCurrent;
            dbCar.ChargerPilotCurrent = car.ChargerPilotCurrent;
            dbCar.ChargerRequestedCurrent = car.ChargerRequestedCurrent;
            dbCar.PluggedIn = car.PluggedIn;
            dbCar.ClimateOn = car.ClimateOn;
            dbCar.Latitude = car.Latitude;
            dbCar.Longitude = car.Longitude;
            dbCar.State = car.State;
            await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        }
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task AddCachedCarStatesToCars(List<DtoCar> cars)
    {
        foreach (var car in cars)
        {
            var cachedCarState = await teslaSolarChargerContext.CachedCarStates
                .FirstOrDefaultAsync(c => c.CarId == car.Id && c.Key == constants.CarStateKey).ConfigureAwait(false);
            if (cachedCarState == default)
            {
                logger.LogWarning("No cached car state found for car with id {carId}", car.Id);
                continue;
            }

            var carState = JsonConvert.DeserializeObject<DepricatedCarState>(cachedCarState.CarStateJson ?? string.Empty);
            if (carState == null)
            {
                logger.LogWarning("Could not deserialized cached car state for car with id {carId}", car.Id);
                continue;
            }

            car.Name = carState.Name;
            car.SoC = carState.SoC;
            car.SocLimit = carState.SocLimit;
            car.ChargerPhases = carState.ChargerPhases;
            car.ChargerVoltage = carState.ChargerVoltage;
            car.ChargerActualCurrent = carState.ChargerActualCurrent;
            car.ChargerPilotCurrent = carState.ChargerPilotCurrent;
            car.ChargerRequestedCurrent = carState.ChargerRequestedCurrent;
            car.PluggedIn = carState.PluggedIn;
            car.ClimateOn = carState.ClimateOn;
            car.Latitude = carState.Latitude;
            car.Longitude = carState.Longitude;
        }
    }

    [SuppressMessage("ReSharper.DPA", "DPA0007: Large number of DB records", MessageId = "count: 1000")]
    public async Task UpdateAverageGridVoltage()
    {
        logger.LogTrace("{method}()", nameof(UpdateAverageGridVoltage));
        var homeGeofence = configurationWrapper.GeoFence();
        const int lowestWorldWideGridVoltage = 100;
        const int voltageBuffer = 15;
        const int lowestGridVoltageToSearchFor = lowestWorldWideGridVoltage - voltageBuffer;
        try
        {
            var chargerVoltages = await teslamateContext
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
                logger.LogDebug("Use {averageVoltage}V for charge speed calculation", averageValue);
                settings.AverageHomeGridVoltage = averageValue;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not detect average grid voltage.");
        }
    }
}
