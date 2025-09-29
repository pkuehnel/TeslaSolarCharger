using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
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
    ICarPropertyUpdateHelper carPropertyUpdateHelper,
    IDateTimeProvider dateTimeProvider)
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
            .Where(c => c.IsAvailableInTeslaAccount || (c.CarType != CarType.Tesla))
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
                CarType = c.CarType,
                MaximumPhases = c.MaximumPhases,
            })
            .ToListAsync().ConfigureAwait(false);

        return cars;
    }

    public ISettings GetSettings()
    {
        logger.LogTrace("{method}()", nameof(GetSettings));
        return settings;
    }

    public async Task AddCarsToSettings(bool initializeManualCarValues = false, int? manualCarIdToInitialize = null)
    {
        settings.Cars = await GetCars(initializeManualCarValues, manualCarIdToInitialize).ConfigureAwait(false);
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
        var databaseCar = carId == default ? new() : await teslaSolarChargerContext.Cars.FirstAsync(c => c.Id == carId);
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
        databaseCar.CarType = carBasicConfiguration.CarType;
        if (carBasicConfiguration.UseFleetTelemetry)
        {
            databaseCar.IsFleetTelemetryHardwareIncompatible = false;
        }
        databaseCar.IncludeTrackingRelevantFields = carBasicConfiguration.IncludeTrackingRelevantFields;
        databaseCar.HomeDetectionVia = carBasicConfiguration.HomeDetectionVia;
        databaseCar.MaximumPhases = carBasicConfiguration.MaximumPhases;
        if (carId == default)
        {
            databaseCar.ChargeMode = ChargeModeV2.Auto;
            databaseCar.MinimumSoc = 10;
            databaseCar.MaximumSoc = 100;
            teslaSolarChargerContext.Cars.Add(databaseCar);
        }
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        var shouldInitializeManualCarValues = carId == default && databaseCar.CarType == CarType.Manual;
        var manualCarIdToInitialize = shouldInitializeManualCarValues ? databaseCar.Id : (int?)null;
        await AddCarsToSettings(shouldInitializeManualCarValues, manualCarIdToInitialize).ConfigureAwait(false);
        if (databaseCar.CarType == CarType.Tesla)
        {
            await fleetTelemetryConfigurationService.SetFleetTelemetryConfiguration(databaseCar.Vin, false);
        }
    }

    private async Task<List<DtoCar>> GetCars(bool initializeManualCarValues, int? manualCarIdToInitialize)
    {
        logger.LogTrace("{method}()", nameof(GetCars));
        var carData = await teslaSolarChargerContext.Cars
            .Select(c => new
            {
                Car = new DtoCar()
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
                },
                c.CarType,
            })
            .ToListAsync().ConfigureAwait(false);

        var cars = carData.Select(c => c.Car).ToList();

        HashSet<int> manualCarIdsToInitialize;
        if (!initializeManualCarValues)
        {
            manualCarIdsToInitialize = new HashSet<int>();
        }
        else if (manualCarIdToInitialize.HasValue)
        {
            manualCarIdsToInitialize = carData
                .Where(c => c.Car.Id == manualCarIdToInitialize.Value && c.CarType == CarType.Manual)
                .Select(c => c.Car.Id)
                .ToHashSet();
        }
        else
        {
            manualCarIdsToInitialize = carData
                .Where(c => c.CarType == CarType.Manual)
                .Select(c => c.Car.Id)
                .ToHashSet();
        }

        foreach (var carDataItem in carData)
        {
            var dtoCar = carDataItem.Car;

            var latestValues = await teslaSolarChargerContext.CarValueLogs
                .Where(c => c.CarId == dtoCar.Id
                            && (c.Type == CarValueType.IsPluggedIn
                                || c.Type == CarValueType.StateOfCharge
                                || c.Type == CarValueType.StateOfChargeLimit
                                || c.Type == CarValueType.ChargerPhases
                                || c.Type == CarValueType.ChargeAmps
                                || c.Type == CarValueType.ChargerPilotCurrent
                                || c.Type == CarValueType.ChargerVoltage
                                || c.Type == CarValueType.ChargeCurrentRequest
                                || c.Type == CarValueType.ModuleTempMin
                                || c.Type == CarValueType.ModuleTempMax
                                || c.Type == CarValueType.IsCharging
                                || c.Type == CarValueType.AsleepOrOffline
                                || c.Type == CarValueType.Latitude
                                || c.Type == CarValueType.Longitude
                                || c.Type == CarValueType.LocatedAtHome
                                || c.Type == CarValueType.LocatedAtWork
                                || c.Type == CarValueType.LocatedAtFavorite
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
                                && c.Timestamp < latestValue.Timestamp
                                && c.DoubleValue != null
                                // ReSharper disable once CompareOfFloatsByEqualityOperator
                                && (c.DoubleValue != latestValue.DoubleValue
                                    || c.IntValue != latestValue.IntValue
                                    || c.StringValue != latestValue.StringValue
                                    || c.UnknownValue != latestValue.UnknownValue
                                    || c.BooleanValue != latestValue.BooleanValue
                                    || c.InvalidValue != latestValue.InvalidValue))
                    .OrderByDescending(c => c.Timestamp)
                    .FirstOrDefaultAsync();
                if (valueBeforeLatestValue != default)
                {
                    UpdateCarPropertyValue(dtoCar, valueBeforeLatestValue, fleetTelemetryConfiguration);
                }
                UpdateCarPropertyValue(dtoCar, latestValue, fleetTelemetryConfiguration);
            }

            if (manualCarIdsToInitialize.Contains(dtoCar.Id))
            {
                await InitializeManualCarValuesAsync(dtoCar).ConfigureAwait(false);
            }
            //Do not trigger car state changed here as app will crash when finding cars in settings
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

    private async Task InitializeManualCarValuesAsync(DtoCar dtoCar)
    {
        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
        dtoCar.IsHomeGeofence.Update(currentDate, true);

        var carValueLogs = new List<CarValueLog>();

        if (dtoCar.PluggedIn.Value == true)
        {
            dtoCar.PluggedIn.Update(currentDate, false);
            carValueLogs.Add(new CarValueLog
            {
                CarId = dtoCar.Id,
                Type = CarValueType.IsPluggedIn,
                BooleanValue = false,
                Timestamp = currentDate.UtcDateTime,
                Source = CarValueSource.Estimation,
            });
        }

        if (dtoCar.IsCharging.Value == true)
        {
            dtoCar.IsCharging.Update(currentDate, false);
            carValueLogs.Add(new CarValueLog
            {
                CarId = dtoCar.Id,
                Type = CarValueType.IsCharging,
                BooleanValue = false,
                Timestamp = currentDate.UtcDateTime,
                Source = CarValueSource.Estimation,
            });
        }

        dtoCar.SoC.Update(currentDate, null);
        if (carValueLogs.Count == 0)
        {
            return;
        }

        teslaSolarChargerContext.CarValueLogs.AddRange(carValueLogs);
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
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
                if (fleetTelemetryConfiguration.HomeDetectionVia == HomeDetectionVia.LocatedAtHome)
                {
                    dtoCar.IsHomeGeofence.Update(new DateTimeOffset(logValue.Timestamp, TimeSpan.Zero),
                        logValue.BooleanValue == true);
                }
                return;
            }
            if (logValue.Type == CarValueType.LocatedAtFavorite)
            {
                if (fleetTelemetryConfiguration.HomeDetectionVia == HomeDetectionVia.LocatedAtFavorite)
                {
                    dtoCar.IsHomeGeofence.Update(new DateTimeOffset(logValue.Timestamp, TimeSpan.Zero),
                        logValue.BooleanValue == true);
                }
                return;
            }
            if (logValue.Type == CarValueType.LocatedAtWork)
            {
                if (fleetTelemetryConfiguration.HomeDetectionVia == HomeDetectionVia.LocatedAtWork)
                {
                    dtoCar.IsHomeGeofence.Update(new DateTimeOffset(logValue.Timestamp, TimeSpan.Zero),
                        logValue.BooleanValue == true);
                }
                return;
            }
        }

        if (logValue.Type == CarValueType.AsleepOrOffline)
        {
            dtoCar.IsOnline.Update(new DateTimeOffset(logValue.Timestamp, TimeSpan.Zero), logValue.BooleanValue == false);
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

    /// <summary>
    /// Add all exisiting cars in Tesla account to allowed cars of all OCPP charging connectors.
    /// </summary>
    /// <returns></returns>
    public async Task AddAllTeslasToAllowedCars()
    {
        logger.LogTrace("{method}()", nameof(AddAllTeslasToAllowedCars));
        var teslasAddedToAllowedCars =
            await teslaSolarChargerContext.TscConfigurations.AnyAsync(c => c.Key == constants.TeslasAddedToAllowedCars).ConfigureAwait(false);
        if (teslasAddedToAllowedCars)
        {
            return;
        }
        var ocppConnectors = await teslaSolarChargerContext.OcppChargingStationConnectors
            .Include(c => c.AllowedCars)
            .ToListAsync();
        var carIdAvailableInTeslaAccount = await teslaSolarChargerContext.Cars
            .Where(c => c.IsAvailableInTeslaAccount && c.ShouldBeManaged == true)
            .Select(c => c.Id)
            .ToHashSetAsync().ConfigureAwait(false);
        foreach (var ocppConnector in ocppConnectors)
        {
            foreach (var carId in carIdAvailableInTeslaAccount)
            {
                if (ocppConnector.AllowedCars.Any(c => c.CarId == carId))
                {
                    continue;
                }
                ocppConnector.AllowedCars.Add(new()
                {
                    CarId = carId,
                });
            }
        }
        teslaSolarChargerContext.TscConfigurations.Add(new()
        {
            Key = constants.TeslasAddedToAllowedCars,
            Value = "true",
        });
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
            var chargingConnectorVoltages = await teslaSolarChargerContext.OcppChargingStationConnectorValueLogs
                .Where(c => (c.Type == OcppChargingStationConnectorValueType.ChargerVoltage)
                            && (c.IntValue > lowestGridVoltageToSearchFor))
                .OrderByDescending(c => c.Id)
                .Select(c => c.IntValue)
                .Take(1000)
                .ToListAsync().ConfigureAwait(false);
            if (chargingConnectorVoltages.Count > 10)
            {
                var averageValue = Convert.ToInt32(chargingConnectorVoltages.Average(c => c!.Value));
                logger.LogDebug("Use {averageVoltage}V for charge speed calculation (provided by charging connector voltages)", averageValue);
                settings.AverageHomeGridVoltage = averageValue;
                return;
            }

            var carVoltages = await teslaSolarChargerContext.CarValueLogs
                .Where(c => (c.Type == CarValueType.ChargerVoltage)
                            && (c.IntValue > lowestGridVoltageToSearchFor))
                .OrderByDescending(c => c.Id)
                .Select(c => c.IntValue)
                .Take(1000)
                .ToListAsync().ConfigureAwait(false);
            if (carVoltages.Count > 10)
            {
                var averageValue = Convert.ToInt32(carVoltages.Average(c => c!.Value));
                logger.LogDebug("Use {averageVoltage}V for charge speed calculation (provided by car voltages)", averageValue);
                settings.AverageHomeGridVoltage = averageValue;
                return;
            }

            var chargingDetailsChargerVoltages = await teslaSolarChargerContext.ChargingDetails
                .Where(c => (c.ChargerVoltage != null)
                            && (c.ChargerVoltage > lowestGridVoltageToSearchFor))
                .OrderByDescending(c => c.Id)
                .Select(c => c.ChargerVoltage)
                .Take(1000)
                .ToListAsync().ConfigureAwait(false);
            if (chargingDetailsChargerVoltages.Count > 10)
            {
                var averageValue = Convert.ToInt32(chargingDetailsChargerVoltages.Average(c => c!.Value));
                logger.LogDebug("Use {averageVoltage}V for charge speed calculation (provided by charging details)", averageValue);
                settings.AverageHomeGridVoltage = averageValue;
                return;
            }

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not detect average grid voltage.");
        }
    }
}
