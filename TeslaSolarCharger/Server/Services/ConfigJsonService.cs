using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Helper.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;

[assembly: InternalsVisibleTo("TeslaSolarCharger.Tests")]
namespace TeslaSolarCharger.Server.Services;

public class ConfigJsonService(
    ILogger<ConfigJsonService> logger,
    ISettings settings,
    IConfigurationWrapper configurationWrapper,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    IConstants constants,
    ITeslaMateDbContextWrapper teslaMateDbContextWrapper,
    IFleetTelemetryConfigurationService fleetTelemetryConfigurationService,
    ITscConfigurationService tscConfigurationService,
    ILoadPointManagementService loadPointManagementService,
    ICarPropertyUpdateHelper carPropertyUpdateHelper)
    : IConfigJsonService
{

    public async Task ConvertOldCarsToNewCar()
    {
        logger.LogTrace("{method}()", nameof(ConvertOldCarsToNewCar));
        await ConvertHandledChargesCarIdsIfNeeded().ConfigureAwait(false);
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

    public async Task<List<CarBasicConfiguration>> GetCarBasicConfigurations()
    {
        logger.LogTrace("{method}()", nameof(GetCarBasicConfigurations));

        var cars = await teslaSolarChargerContext.Cars
            .Where(c => c.IsAvailableInTeslaAccount)
            .OrderBy(c => c.ChargingPriority)
            .Select(c => new CarBasicConfiguration(c.Id, c.Name)
            {
                Vin = c.Vin ?? string.Empty,
                MinimumAmpere = c.MinimumAmpere,
                SwitchOffAtCurrent = c.SwitchOffAtCurrent,
                SwitchOnAtCurrent = c.SwitchOnAtCurrent,
                MaximumAmpere = c.MaximumAmpere,
                UsableEnergy = c.UsableEnergy,
                ChargingPriority = c.ChargingPriority,
                ShouldBeManaged = c.ShouldBeManaged == true,
                UseBle = c.UseBle,
                BleApiBaseUrl = c.BleApiBaseUrl,
                UseFleetTelemetry = c.UseFleetTelemetry,
                IncludeTrackingRelevantFields = c.IncludeTrackingRelevantFields,
                HomeDetectionVia = c.HomeDetectionVia,
            })
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
        foreach (var dtoCar in settings.CarsToManage)
        {
            await loadPointManagementService.CarStateChanged(dtoCar.Id);
        }
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

    public async Task SetCorrectHomeDetectionVia()
    {
        logger.LogTrace("{method}()", nameof(SetCorrectHomeDetectionVia));
        var homeDetectionViaConvertedValue = await tscConfigurationService
            .GetConfigurationValueByKey(constants.HomeDetectionViaConvertedKey).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(homeDetectionViaConvertedValue))
        {
            logger.LogDebug("Home detection via already converted");
            return;
        }

        var cars = await teslaSolarChargerContext.Cars.ToListAsync().ConfigureAwait(false);
        foreach (var car in cars)
        {
            if (car.UseFleetTelemetry
                && !car.IncludeTrackingRelevantFields
                && configurationWrapper.GetVehicleDataFromTesla())
            {
                car.HomeDetectionVia = HomeDetectionVia.LocatedAtHome;
            }
            else
            {
                car.HomeDetectionVia = HomeDetectionVia.GpsLocation;
            }
        }
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        await tscConfigurationService.SetConfigurationValueByKey(constants.HomeDetectionViaConvertedKey, "true").ConfigureAwait(false);
    }

    public async Task UpdateCarBasicConfiguration(int carId, CarBasicConfiguration carBasicConfiguration)
    {
        logger.LogTrace("{method}({carId}, {@carBasicConfiguration})", nameof(UpdateCarBasicConfiguration), carId, carBasicConfiguration);
        var databaseCar = await teslaSolarChargerContext.Cars.FirstAsync(c => c.Id == carId);
        databaseCar.Name = carBasicConfiguration.Name;
        databaseCar.Vin = carBasicConfiguration.Vin;
        databaseCar.MinimumAmpere = carBasicConfiguration.MinimumAmpere;
        databaseCar.SwitchOffAtCurrent = carBasicConfiguration.SwitchOffAtCurrent;
        databaseCar.SwitchOnAtCurrent = carBasicConfiguration.SwitchOnAtCurrent;
        databaseCar.MaximumAmpere = carBasicConfiguration.MaximumAmpere;
        databaseCar.UsableEnergy = carBasicConfiguration.UsableEnergy;
        databaseCar.ChargingPriority = carBasicConfiguration.ChargingPriority;
        databaseCar.ShouldBeManaged = carBasicConfiguration.ShouldBeManaged;
        databaseCar.UseBle = carBasicConfiguration.UseBle;
        databaseCar.BleApiBaseUrl = carBasicConfiguration.BleApiBaseUrl;
        databaseCar.UseFleetTelemetry = carBasicConfiguration.UseFleetTelemetry;
        if (carBasicConfiguration.UseFleetTelemetry)
        {
            databaseCar.IsFleetTelemetryHardwareIncompatible = false;
        }
        databaseCar.IncludeTrackingRelevantFields = carBasicConfiguration.IncludeTrackingRelevantFields;
        databaseCar.HomeDetectionVia = carBasicConfiguration.HomeDetectionVia;
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
        await fleetTelemetryConfigurationService.SetFleetTelemetryConfiguration(settingsCar.Vin, false);
    }

    private async Task<List<DtoCar>> GetCars()
    {
        logger.LogTrace("{method}()", nameof(GetCars));
        var cars = await teslaSolarChargerContext.Cars
            .Select(c => new DtoCar()
            {
                Id = c.Id,
                Vin = c.Vin ?? string.Empty,
                TeslaMateCarId = c.TeslaMateCarId,
                ChargeModeV2 = c.ChargeMode,
                MinimumSoC = c.MinimumSoc,
                MaximumAmpere = c.MaximumAmpere,
                MinimumAmpere = c.MinimumAmpere,
                UsableEnergy = c.UsableEnergy,
                ShouldBeManaged = c.ShouldBeManaged,
                ChargingPriority = c.ChargingPriority,
                Name = c.Name,
                UseBle = c.UseBle,
                BleApiBaseUrl = c.BleApiBaseUrl,
                WakeUpCalls = c.WakeUpCalls,
                VehicleDataCalls = c.VehicleDataCalls,
                VehicleCalls = c.VehicleCalls,
                ChargeStartCalls = c.ChargeStartCalls,
                ChargeStopCalls = c.ChargeStopCalls,
                SetChargingAmpsCall = c.SetChargingAmpsCall,
                OtherCommandCalls = c.OtherCommandCalls,
            })
            .ToListAsync().ConfigureAwait(false);
        foreach (var dtoCar in cars)
        {
            var latestValues = await teslaSolarChargerContext.CarValueLogs
                .Where(c => c.CarId == dtoCar.Id
                            && (c.Type == CarValueType.IsPluggedIn
                                || c.Type == CarValueType.StateOfCharge
                                || c.Type == CarValueType.StateOfChargeLimit
                                || c.Type == CarValueType.ChargerPhases
                                || c.Type == CarValueType.ChargeAmps
                                || c.Type == CarValueType.ChargerPilotCurrent
                                || c.Type == CarValueType.ChargeCurrentRequest
                                || c.Type == CarValueType.ModuleTempMin
                                || c.Type == CarValueType.ModuleTempMax
                                || c.Type == CarValueType.IsCharging
                                || c.Type == CarValueType.AsleepOrOffline
                                || c.Type == CarValueType.Latitude
                                || c.Type == CarValueType.Longitude
                            ))
                .GroupBy(c => c.Type)
                .Select(g => g.OrderByDescending(c => c.Timestamp).First())
                .AsNoTracking()
                .ToListAsync();
            var fleetTelemetryConfiguration = await teslaSolarChargerContext.Cars
                .Where(c => c.Id == dtoCar.Id)
                .Select(c => new FleetTelemetryConfiguration()
                {
                    UseFleetTelemetry = c.UseFleetTelemetry,
                    IncludeTrackingRelevantFields = c.IncludeTrackingRelevantFields,
                    HomeDetectionVia = c.HomeDetectionVia,
                })
                .FirstOrDefaultAsync();

            foreach (var latestValue in latestValues)
            {
                var valueBeforeLatestValue = await teslaSolarChargerContext.CarValueLogs
                    .Where(c => c.CarId == dtoCar.Id
                                && c.Type == latestValue.Type
                                && c.Timestamp < latestValue.Timestamp)
                    .OrderByDescending(c => c.Timestamp)
                    .FirstOrDefaultAsync();
                if (valueBeforeLatestValue != default)
                {
                    UpdateCarPropertyValue(dtoCar, valueBeforeLatestValue, fleetTelemetryConfiguration);
                }
                UpdateCarPropertyValue(dtoCar, latestValue, fleetTelemetryConfiguration);
            }
        }
        var teslaMateContext = teslaMateDbContextWrapper.GetTeslaMateContextIfAvailable();
        if (configurationWrapper.UseTeslaMateIntegration() && (teslaMateContext != default))
        {
            foreach (var car in cars)
            {
                if (!string.IsNullOrEmpty(car.Vin))
                {
                    var teslaMateCarId = await teslaMateContext.Cars
                        .Where(c => c.Vin == car.Vin)
                        .Select(c => c.Id)
                        .FirstOrDefaultAsync();
                    if (teslaMateCarId != default && car.TeslaMateCarId != teslaMateCarId)
                    {
                        var dbCar = await teslaSolarChargerContext.Cars.FirstAsync(c => c.Id == car.Id);
                        dbCar.TeslaMateCarId = teslaMateCarId;
                        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
                        car.TeslaMateCarId = teslaMateCarId;
                    }
                }
            }
        }
        return cars;
    }

    private class FleetTelemetryConfiguration
    {
        public bool UseFleetTelemetry { get; set; }
        public bool IncludeTrackingRelevantFields { get; set; }
        public HomeDetectionVia HomeDetectionVia { get; set; }
    }

    private void UpdateCarPropertyValue(DtoCar dtoCar, CarValueLog logValue, FleetTelemetryConfiguration? fleetTelemetryConfiguration)
    {
        logger.LogTrace("{method}({carId}, {@logValue})", nameof(UpdateCarPropertyValue), dtoCar.Id, logValue);
        if (logValue.Type is CarValueType.LocatedAtHome or CarValueType.LocatedAtFavorite or CarValueType.LocatedAtWork)
        {
            if (fleetTelemetryConfiguration == default)
            {
                return;
            }

            if (logValue.Type == CarValueType.LocatedAtHome)
            {
                if (fleetTelemetryConfiguration.HomeDetectionVia != HomeDetectionVia.LocatedAtHome)
                {
                    return;
                }
            }
            if (logValue.Type == CarValueType.LocatedAtFavorite)
            {
                if (fleetTelemetryConfiguration.HomeDetectionVia != HomeDetectionVia.LocatedAtFavorite)
                {
                    return;
                }
            }
            if (logValue.Type == CarValueType.LocatedAtWork)
            {
                if (fleetTelemetryConfiguration.HomeDetectionVia != HomeDetectionVia.LocatedAtWork)
                {
                    return;
                }
            }
        }
        carPropertyUpdateHelper.UpdateDtoCarProperty(dtoCar, logValue);
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
            dbCar.WakeUpCalls = car.WakeUpCalls;
            dbCar.VehicleDataCalls = car.VehicleDataCalls;
            dbCar.VehicleCalls = car.VehicleCalls;
            dbCar.ChargeStartCalls = car.ChargeStartCalls;
            dbCar.ChargeStopCalls = car.ChargeStopCalls;
            dbCar.SetChargingAmpsCall = car.SetChargingAmpsCall;
            dbCar.OtherCommandCalls = car.OtherCommandCalls;


            await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        }
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }

    [SuppressMessage("ReSharper.DPA", "DPA0007: Large number of DB records", MessageId = "count: 1000")]
    public async Task UpdateAverageGridVoltage()
    {
        logger.LogTrace("{method}()", nameof(UpdateAverageGridVoltage));
        const int lowestWorldWideGridVoltage = 100;
        const int voltageBuffer = 15;
        const int lowestGridVoltageToSearchFor = lowestWorldWideGridVoltage - voltageBuffer;
        try
        {
            var chargerVoltages = await teslaSolarChargerContext.ChargingDetails
                .Where(c => (c.ChargerVoltage != null)
                            && (c.ChargerVoltage > lowestGridVoltageToSearchFor))
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
