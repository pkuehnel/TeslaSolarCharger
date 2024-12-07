using AutoMapper.QueryableExtensions;
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
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedBackend.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.IndexRazor.CarValues;
using TeslaSolarCharger.Model.EntityFramework;
using TeslaSolarCharger.SharedBackend.MappingExtensions;
using System;
using TeslaSolarCharger.Server.Scheduling;
using TeslaSolarCharger.Server.Services.Contracts;

[assembly: InternalsVisibleTo("TeslaSolarCharger.Tests")]
namespace TeslaSolarCharger.Server.Services;

public class ConfigJsonService(
    ILogger<ConfigJsonService> logger,
    ISettings settings,
    IConfigurationWrapper configurationWrapper,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    IConstants constants,
    IDateTimeProvider dateTimeProvider,
    IMapperConfigurationFactory mapperConfigurationFactory,
    JobManager jobManager,
    ITeslaMateDbContextWrapper teslaMateDbContextWrapper,
    IFleetTelemetryWebSocketService fleetTelemetryWebSocketService)
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
        await ConvertCarConfigurationsIncludingCarStatesIfNeeded().ConfigureAwait(false);

        await ConvertHandledChargesCarIdsIfNeeded().ConfigureAwait(false);
    }

    private async Task ConvertCarConfigurationsIncludingCarStatesIfNeeded()
    {
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
        var teslaMateContext = teslaMateDbContextWrapper.GetTeslaMateContextIfAvailable();
        if (oldCarConfiguration.Count > 0 && teslaMateContext != default)
        {
            
            foreach (var databaseCarConfiguration in oldCarConfiguration)
            {
                var configuration =
                    JsonConvert.DeserializeObject<DepricatedCarConfiguration>(databaseCarConfiguration.CarStateJson ?? string.Empty);
                if (configuration == default)
                {
                    continue;
                }
                
                var teslaMateDatabaseCar = await teslaMateContext.Cars.FirstOrDefaultAsync(c => c.Id == databaseCarConfiguration.CarId)
                    .ConfigureAwait(false);
                if (teslaMateDatabaseCar == default)
                {
                    logger.LogError("Car with id {carId} not found in teslamate database. Can not be converted.", databaseCarConfiguration.CarId);
                    continue;
                }
                cars.Add(new DtoCar()
                {
                    Vin = teslaMateDatabaseCar.Vin ?? string.Empty,
                    Name = teslaMateDatabaseCar.Name ?? string.Empty,
                    TeslaMateCarId = databaseCarConfiguration.CarId,
                    ChargeMode = configuration.ChargeMode,
                    MinimumSoC = configuration.MinimumSoC,
                    LatestTimeToReachSoC = configuration.LatestTimeToReachSoC,
                    IgnoreLatestTimeToReachSocDate = configuration.IgnoreLatestTimeToReachSocDate,
                    IgnoreLatestTimeToReachSocDateOnWeekend = configuration.IgnoreLatestTimeToReachSocDateOnWeekend,
                    MaximumAmpere = configuration.MaximumAmpere,
                    MinimumAmpere = configuration.MinimumAmpere,
                    UsableEnergy = configuration.UsableEnergy,
                    ShouldBeManaged = configuration.ShouldBeManaged,
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
        }
    }

    private async Task ConvertHandledChargesCarIdsIfNeeded()
    {
        var handledChargesCarIdsConverted =
            await teslaSolarChargerContext.TscConfigurations.AnyAsync(c => c.Key == constants.HandledChargesCarIdsConverted).ConfigureAwait(false);
        if (!handledChargesCarIdsConverted)
        {
            var carIdsToChange = await teslaSolarChargerContext.Cars
                .Where(c => c.Id != c.TeslaMateCarId)
                .Select(c => new
                {
                    c.TeslaMateCarId,
                    c.Id,
                })
                .ToListAsync().ConfigureAwait(false);
            if (carIdsToChange.Count < 1)
            {
                return;
            }
            var handledCharges = await teslaSolarChargerContext.HandledCharges.ToListAsync().ConfigureAwait(false);
            foreach (var handledCharge in handledCharges)
            {
                if (carIdsToChange.Any(c => c.TeslaMateCarId == handledCharge.CarId))
                {
                    handledCharge.CarId = carIdsToChange.First(c => c.TeslaMateCarId == handledCharge.CarId).Id;
                }
            }
            teslaSolarChargerContext.TscConfigurations.Add(new TscConfiguration()
            {
                Key = constants.HandledChargesCarIdsConverted,
                Value = "true",
            });
            await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    public async Task UpdateCarBaseSettings(DtoCarBaseSettings carBaseSettings)
    {
        logger.LogTrace("{method}({@carBaseSettings})", nameof(UpdateCarBaseSettings), carBaseSettings);
        var databaseCar = await teslaSolarChargerContext.Cars.FirstAsync(c => c.Id == carBaseSettings.CarId).ConfigureAwait(false);
        databaseCar.ChargeMode = carBaseSettings.ChargeMode;
        databaseCar.MinimumSoc = carBaseSettings.MinimumStateOfCharge;
        databaseCar.LatestTimeToReachSoC = carBaseSettings.LatestTimeToReachStateOfCharge;
        databaseCar.IgnoreLatestTimeToReachSocDate = carBaseSettings.IgnoreLatestTimeToReachSocDate;
        databaseCar.IgnoreLatestTimeToReachSocDateOnWeekend = carBaseSettings.IgnoreLatestTimeToReachSocDateOnWeekend;
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        var settingsCar = settings.Cars.First(c => c.Id == carBaseSettings.CarId);
        settingsCar.ChargeMode = carBaseSettings.ChargeMode;
        settingsCar.MinimumSoC = carBaseSettings.MinimumStateOfCharge;
        settingsCar.LatestTimeToReachSoC = carBaseSettings.LatestTimeToReachStateOfCharge;
        settingsCar.IgnoreLatestTimeToReachSocDate = carBaseSettings.IgnoreLatestTimeToReachSocDate;
        settingsCar.IgnoreLatestTimeToReachSocDateOnWeekend = carBaseSettings.IgnoreLatestTimeToReachSocDateOnWeekend;


    }

    public async Task<List<CarBasicConfiguration>> GetCarBasicConfigurations()
    {
        logger.LogTrace("{method}()", nameof(GetCarBasicConfigurations));

        var mapper = mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<Car, CarBasicConfiguration>()
            ;
        });

        var cars = await teslaSolarChargerContext.Cars
            .ProjectTo<CarBasicConfiguration>(mapper)
            .ToListAsync().ConfigureAwait(false);

        return cars;
    }

    public ISettings GetSettings()
    {
        logger.LogTrace("{method}()", nameof(GetSettings));
        return settings;
    }

    public async Task AddCarsToSettings()
    {
        settings.Cars = await GetCars().ConfigureAwait(false);
    }

    public async Task AddBleBaseUrlToAllCars()
    {
        logger.LogTrace("{method}()", nameof(AddBleBaseUrlToAllCars));
        var bleBaseUrlConverted =
            await teslaSolarChargerContext.TscConfigurations.AnyAsync(c => c.Key == constants.BleBaseUrlConverted).ConfigureAwait(false);
        if (bleBaseUrlConverted)
        {
            return;
        }
        var baseUrl = configurationWrapper.BleBaseUrl();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return;
        }
        if (baseUrl.EndsWith("api/"))
        {
            baseUrl = baseUrl.Substring(0, baseUrl.Length - "api/".Length);
        }
        var databaseCars = await teslaSolarChargerContext.Cars.ToListAsync().ConfigureAwait(false);
        foreach (var car in databaseCars)
        {
            car.BleApiBaseUrl = baseUrl;
        }
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        teslaSolarChargerContext.TscConfigurations.Add(new TscConfiguration()
        {
            Key = constants.BleBaseUrlConverted,
            Value = "true",
        });
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task UpdateCarBasicConfiguration(int carId, CarBasicConfiguration carBasicConfiguration)
    {
        logger.LogTrace("{method}({carId}, {@carBasicConfiguration})", nameof(UpdateCarBasicConfiguration), carId, carBasicConfiguration);
        var databaseCar = await teslaSolarChargerContext.Cars.FirstAsync(c => c.Id == carId);
        databaseCar.Name = carBasicConfiguration.Name;
        databaseCar.Vin = carBasicConfiguration.Vin;
        databaseCar.MinimumAmpere = carBasicConfiguration.MinimumAmpere;
        databaseCar.MaximumAmpere = carBasicConfiguration.MaximumAmpere;
        databaseCar.UsableEnergy = carBasicConfiguration.UsableEnergy;
        databaseCar.ChargingPriority = carBasicConfiguration.ChargingPriority;
        databaseCar.ShouldBeManaged = carBasicConfiguration.ShouldBeManaged;
        databaseCar.UseBle = carBasicConfiguration.UseBle;
        databaseCar.BleApiBaseUrl = carBasicConfiguration.BleApiBaseUrl;
        databaseCar.UseFleetTelemetry = carBasicConfiguration.UseFleetTelemetry;
        databaseCar.UseFleetTelemetryForLocationData = carBasicConfiguration.UseFleetTelemetryForLocationData;
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        var settingsCar = settings.Cars.First(c => c.Id == carId);
        settingsCar.Name = carBasicConfiguration.Name;
        settingsCar.Vin = carBasicConfiguration.Vin;
        settingsCar.MinimumAmpere = carBasicConfiguration.MinimumAmpere;
        settingsCar.MaximumAmpere = carBasicConfiguration.MaximumAmpere;
        settingsCar.UsableEnergy = carBasicConfiguration.UsableEnergy;
        settingsCar.ChargingPriority = carBasicConfiguration.ChargingPriority;
        settingsCar.ShouldBeManaged = carBasicConfiguration.ShouldBeManaged;
        settingsCar.UseBle = carBasicConfiguration.UseBle;
        settingsCar.BleApiBaseUrl = carBasicConfiguration.BleApiBaseUrl;
        await fleetTelemetryWebSocketService.DisconnectWebSocketsByVin(carBasicConfiguration.Vin);
    }

    public Task UpdateCarConfiguration(int carId, DepricatedCarConfiguration carConfiguration)
    {
        throw new NotImplementedException();
    }

    public async Task SaveOrUpdateCar(DtoCar car)
    {
        var entity = teslaSolarChargerContext.Cars.FirstOrDefault(c => c.TeslaMateCarId == car.TeslaMateCarId) ?? new Car()
        {
            Id = car.Id,
            
        };
        var teslaMateContext = teslaMateDbContextWrapper.GetTeslaMateContextIfAvailable();
        if (teslaMateContext != default)
        {
            entity.TeslaMateCarId = teslaMateContext.Cars.FirstOrDefault(c => c.Vin == car.Vin)?.Id ?? default;
        }
        entity.Name = car.Name;
        entity.Vin = car.Vin;
        entity.ChargeMode = car.ChargeMode;
        entity.MinimumSoc = car.MinimumSoC;
        entity.LatestTimeToReachSoC = car.LatestTimeToReachSoC;
        entity.IgnoreLatestTimeToReachSocDate = car.IgnoreLatestTimeToReachSocDate;
        entity.IgnoreLatestTimeToReachSocDateOnWeekend = car.IgnoreLatestTimeToReachSocDateOnWeekend;
        entity.MaximumAmpere = car.MaximumAmpere;
        entity.MinimumAmpere = car.MinimumAmpere;
        entity.UsableEnergy = car.UsableEnergy;
        entity.ShouldBeManaged = car.ShouldBeManaged ?? true;
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

    private async Task<List<DtoCar>> GetCars()
    {
        logger.LogTrace("{method}()", nameof(GetCars));
        var mapper = mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<Car, DtoCar>()
                .ForMember(d => d.WakeUpCalls, opt => opt.MapFrom(s => s.WakeUpCalls == null ? new List<DateTime>() : JsonConvert.DeserializeObject<List<DateTime>>(s.WakeUpCalls)))
                .ForMember(d => d.VehicleDataCalls, opt => opt.MapFrom(s => s.VehicleDataCalls == null ? new List<DateTime>() : JsonConvert.DeserializeObject<List<DateTime>>(s.VehicleDataCalls)))
                .ForMember(d => d.VehicleCalls, opt => opt.MapFrom(s => s.VehicleCalls == null ? new List<DateTime>() : JsonConvert.DeserializeObject<List<DateTime>>(s.VehicleCalls)))
                .ForMember(d => d.ChargeStartCalls, opt => opt.MapFrom(s => s.ChargeStartCalls == null ? new List<DateTime>() : JsonConvert.DeserializeObject<List<DateTime>>(s.ChargeStartCalls)))
                .ForMember(d => d.ChargeStopCalls, opt => opt.MapFrom(s => s.ChargeStopCalls == null ? new List<DateTime>() : JsonConvert.DeserializeObject<List<DateTime>>(s.ChargeStopCalls)))
                .ForMember(d => d.SetChargingAmpsCall, opt => opt.MapFrom(s => s.SetChargingAmpsCall == null ? new List<DateTime>() : JsonConvert.DeserializeObject<List<DateTime>>(s.SetChargingAmpsCall)))
                .ForMember(d => d.OtherCommandCalls, opt => opt.MapFrom(s => s.OtherCommandCalls == null ? new List<DateTime>() : JsonConvert.DeserializeObject<List<DateTime>>(s.OtherCommandCalls)))
                ;
        });
        var cars = await teslaSolarChargerContext.Cars
            .ProjectTo<DtoCar>(mapper)
            .ToListAsync().ConfigureAwait(false);
        return cars;
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
            dbCar.WakeUpCalls = JsonConvert.SerializeObject(car.WakeUpCalls);
            dbCar.VehicleDataCalls = JsonConvert.SerializeObject(car.VehicleDataCalls);
            dbCar.VehicleCalls = JsonConvert.SerializeObject(car.VehicleCalls);
            dbCar.ChargeStartCalls = JsonConvert.SerializeObject(car.ChargeStartCalls);
            dbCar.ChargeStopCalls = JsonConvert.SerializeObject(car.ChargeStopCalls);
            dbCar.SetChargingAmpsCall = JsonConvert.SerializeObject(car.SetChargingAmpsCall);
            dbCar.OtherCommandCalls = JsonConvert.SerializeObject(car.OtherCommandCalls);


            await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        }
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task AddCachedCarStatesToCars(List<DtoCar> cars)
    {
        foreach (var car in cars)
        {
            var cachedCarState = await teslaSolarChargerContext.CachedCarStates
                .FirstOrDefaultAsync(c => c.CarId == car.TeslaMateCarId && c.Key == constants.CarStateKey).ConfigureAwait(false);
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
            var teslaMateContext = teslaMateDbContextWrapper.GetTeslaMateContextIfAvailable();
            if (teslaMateContext != default && configurationWrapper.UseTeslaMateIntegration())
            {
                var chargerVoltages = await teslaMateContext
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
            else
            {
                var chargerVoltages = await teslaSolarChargerContext.ChargingDetails
                    .Where(c => c.ChargerVoltage != null)
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
            
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not detect average grid voltage.");
        }
    }
}
