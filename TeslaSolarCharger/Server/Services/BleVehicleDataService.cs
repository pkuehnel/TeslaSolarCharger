using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PkSoftwareService.Custom.Backend.Ble;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos.Ble;
using TeslaSolarCharger.Server.Helper.Contracts;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class BleVehicleDataService(
    ILogger<BleVehicleDataService> logger,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    ISettings settings,
    IBleService bleService,
    IConfigurationWrapper configurationWrapper,
    IDateTimeProvider dateTimeProvider,
    ICarPropertyUpdateHelper carPropertyUpdateHelper,
    ILoadPointManagementService loadPointManagementService,
    IErrorHandlingService errorHandlingService,
    IBlePresenceStateService blePresenceStateService,
    IIssueKeys issueKeys) : IBleVehicleDataService
{
    private const string AwakeSleepStatus = "VEHICLE_SLEEP_STATUS_AWAKE";
    private const string ChargingStateDisconnected = "Disconnected";
    private const string ChargingStateCharging = "Charging";
    private const string ChargingStateUnknown = "Unknown";
    private static readonly TimeSpan RadioSilenceWarningDuration = TimeSpan.FromHours(24);

    public async Task RefreshBleCarData()
    {
        logger.LogTrace("{method}()", nameof(RefreshBleCarData));
        if (!configurationWrapper.GetVehicleDataViaBle() || !configurationWrapper.GetVehicleDataFromTesla())
        {
            return;
        }
        //Only cars that already switched to BLE data collection (Fleet Telemetry disabled on manual car config save)
        //are refreshed via BLE.
        var bleDataCarIds = await teslaSolarChargerContext.Cars
            .Where(c => (c.ShouldBeManaged == true)
                        && (c.CarType == CarType.Tesla)
                        && c.UseBle
                        && !c.UseFleetTelemetry
                        && !c.IncludeTrackingRelevantFields)
            .Select(c => c.Id)
            .ToListAsync().ConfigureAwait(false);
        var cars = bleDataCarIds
            .Select(carId => settings.Cars.FirstOrDefault(c => c.Id == carId))
            .Where(car => car != default && !string.IsNullOrEmpty(car.Vin))
            .Cast<DtoCar>()
            .ToList();
        //A car that left BLE data collection must not keep a stale uncertain state that would suppress its charging
        //commands forever.
        blePresenceStateService.RetainOnly(cars.Select(c => c.Id).ToList());
        //Cars on different adapters (or different containers) are served by different workers, so their groups can
        //run in parallel; within a group everything serializes on the adapter anyway.
        var groups = cars
            .GroupBy(c => (Host: c.BleApiBaseUrl, Adapter: c.BleAdapterAddress))
            .ToList();
        await Task.WhenAll(groups.Select(group => RefreshGroup(group.Key.Host, group.Key.Adapter, group.ToList()))).ConfigureAwait(false);
    }

    private async Task RefreshGroup(string? host, string? adapter, List<DtoCar> cars)
    {
        logger.LogTrace("{method}({host}, {adapter}, {carCount} cars)", nameof(RefreshGroup), host, adapter, cars.Count);
        var vins = cars.Select(c => c.Vin!).ToList();
        DtoBleBeaconScanResult scanResult;
        try
        {
            //keepWarmSeconds is only ever sent here, on the scheduled poll: the worker of this adapter stays warm
            //between polls, while one-off commands never change the warm window.
            scanResult = await bleService.GetBeaconScanResults(host, adapter, vins, BleConstants.BleKeepWarmSeconds).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Beacon scan for {host} (adapter {adapter}) failed", host, adapter);
            await HandleScanUnavailable(cars, adapter, $"Beacon scan failed: {ex.Message}", isAdapterMissing: false).ConfigureAwait(false);
            return;
        }
        if (!scanResult.Success || scanResult.Outcome != BleCommandOutcome.Ok)
        {
            //The scan itself could not run: this carries no presence information for any car, so the last known
            //state stays valid and only the error is surfaced.
            logger.LogError("Beacon scan for {host} (adapter {adapter}) could not run: {outcome} {message}",
                host, adapter, scanResult.Outcome, scanResult.ResultMessage);
            await HandleScanUnavailable(cars, adapter,
                $"Beacon scan could not run ({scanResult.Outcome}): {scanResult.ResultMessage}",
                isAdapterMissing: scanResult.Outcome == BleCommandOutcome.AdapterNotFound).ConfigureAwait(false);
            return;
        }
        await HandleRadioEvidence(host, adapter, cars, scanResult).ConfigureAwait(false);
        foreach (var car in cars)
        {
            try
            {
                var vehicleResult = scanResult.Vehicles.FirstOrDefault(v => string.Equals(v.Vin, car.Vin, StringComparison.OrdinalIgnoreCase));
                if (vehicleResult is { BeaconFound: true })
                {
                    await RefreshPresentCarData(car).ConfigureAwait(false);
                }
                else
                {
                    await HandleOutOfRangeObservation(car).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while refreshing BLE data for car {vin}", car.Vin);
                await errorHandlingService.HandleError(nameof(BleVehicleDataService), nameof(RefreshBleCarData),
                    $"Error while getting vehicle data via BLE for car {car.Vin}", ex.Message,
                    issueKeys.BleDataCollectionError, car.Vin, ex.StackTrace).ConfigureAwait(false);
                continue;
            }
            try
            {
                await loadPointManagementService.CarStateChanged(car.Id).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while processing CarStateChanged for car ID {carId}", car.Id);
            }
        }
    }

    private async Task HandleScanUnavailable(List<DtoCar> cars, string? adapter, string message, bool isAdapterMissing)
    {
        foreach (var car in cars)
        {
            if (isAdapterMissing)
            {
                //Never a silent fallback to a different radio: a missing configured adapter is an explicit error.
                await errorHandlingService.HandleError(nameof(BleVehicleDataService), nameof(RefreshBleCarData),
                    $"Configured Bluetooth adapter for car {car.Vin} not found",
                    $"The adapter {adapter} is not present on the BLE container's host. Check the adapter selection of the car or replug the adapter.",
                    issueKeys.BleAdapterNotFound, car.Vin, null).ConfigureAwait(false);
                continue;
            }
            await errorHandlingService.HandleError(nameof(BleVehicleDataService), nameof(RefreshBleCarData),
                $"Error while getting vehicle data via BLE for car {car.Vin}", message,
                issueKeys.BleDataCollectionError, car.Vin, null).ConfigureAwait(false);
        }
    }

    private async Task HandleRadioEvidence(string? host, string? adapter, List<DtoCar> cars, DtoBleBeaconScanResult scanResult)
    {
        var heardAnything = scanResult.OtherAdvertisementsSeen > 0
                            || scanResult.DistinctDevicesSeen > 0
                            || scanResult.Vehicles.Any(v => v.BeaconFound);
        var containerKey = $"{host}|{adapter}";
        var silence = blePresenceStateService.RegisterScanEvidence(containerKey, heardAnything, new DateTimeOffset(dateTimeProvider.UtcNow(), TimeSpan.Zero));
        foreach (var car in cars)
        {
            if (heardAnything)
            {
                await errorHandlingService.HandleErrorResolved(issueKeys.BleRadioSilence, car.Vin).ConfigureAwait(false);
            }
            else if (silence > RadioSilenceWarningDuration)
            {
                //Diagnostics only, presence is never touched: at a site with no other Bluetooth devices this is
                //expected while the car is away, but after the site B incident (a starved radio reported a garaged
                //car as away for days) a long fully silent radio is worth a hint.
                await errorHandlingService.HandleError(nameof(BleVehicleDataService), nameof(RefreshBleCarData),
                    $"BLE radio of the container used for car {car.Vin} hears nothing",
                    $"No Bluetooth advertisement of any device was received for {silence.TotalHours:0} hours. If other Bluetooth devices are usually nearby, " +
                    "check the radio: on a Raspberry Pi a poor WiFi link starves Bluetooth because they share one antenna (see the README), " +
                    "or use a USB Bluetooth adapter. If no Bluetooth devices are ever near the container, this message is expected while the car is away.",
                    issueKeys.BleRadioSilence, car.Vin, null).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleOutOfRangeObservation(DtoCar car)
    {
        var vin = car.Vin!;
        var timestamp = dateTimeProvider.UtcNow();
        var confirmation = blePresenceStateService.RegisterOutOfRange(car.Id);
        switch (confirmation)
        {
            case BleAwayConfirmation.JustConfirmed:
                logger.LogInformation("Beacon of car {vin} not found in multiple consecutive scans, car is confirmed as away", vin);
                UpdateHomePresence(car, false, timestamp);
                UpdateOnlineState(car, false, timestamp);
                ResetChargingValuesForAwayCar(car, timestamp);
                await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
                await errorHandlingService.HandleErrorResolved(issueKeys.BleDataCollectionError, vin).ConfigureAwait(false);
                break;
            case BleAwayConfirmation.AlreadyConfirmed:
                //The car is already marked as away: nothing changed, so do not write the same values again.
                break;
            default:
                //Not enough consecutive misses yet: keep the last known state; charging commands are suspended via
                //IsPresenceUncertain until either a hit proves the car is there or the away state is confirmed.
                logger.LogDebug("Beacon of car {vin} not found, keeping last known state until the away state is confirmed", vin);
                break;
        }
    }

    /// <summary>
    /// A car that is confirmed as away can not be plugged in at home anymore: reset the charging values that would
    /// otherwise stay stale until the car is back in BLE range and awake. The state of charge intentionally keeps its
    /// last known value. The values are inferred rather than read from the car, so they are stored with
    /// CarValueSource.Estimation.
    /// </summary>
    private void ResetChargingValuesForAwayCar(DtoCar car, DateTime timestamp)
    {
        AddBooleanValue(car, CarValueType.IsPluggedIn, false, timestamp, CarValueSource.Estimation);
        AddBooleanValue(car, CarValueType.IsCharging, false, timestamp, CarValueSource.Estimation);
        AddIntValue(car, CarValueType.ChargeAmps, 0, timestamp, CarValueSource.Estimation, skipDefaultValue: false);
    }

    /// <summary>
    /// Refreshes the data of a car whose beacon was just seen. Presence is decided by the beacon scan alone: a
    /// failed read directly after a beacon hit is a transient failure of a provably present car and must never count
    /// towards the away confirmation.
    /// </summary>
    private async Task RefreshPresentCarData(DtoCar car)
    {
        logger.LogTrace("{method}({vin})", nameof(RefreshPresentCarData), car.Vin);
        var vin = car.Vin!;
        blePresenceStateService.RegisterSuccessfulRead(car.Id);
        var timestamp = dateTimeProvider.UtcNow();
        //Persist the presence right away in case the body controller read afterwards fails.
        UpdateHomePresence(car, true, timestamp);
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        var bodyControllerStateResult = await bleService.GetBodyControllerState(vin).ConfigureAwait(false);
        timestamp = dateTimeProvider.UtcNow();
        if (!bodyControllerStateResult.Success)
        {
            logger.LogError("Could not get body controller state for car {vin}: {outcome} {resultMessage}", vin,
                bodyControllerStateResult.Outcome, bodyControllerStateResult.ResultMessage);
            await errorHandlingService.HandleError(nameof(BleVehicleDataService), nameof(RefreshPresentCarData),
                $"Error while getting vehicle data via BLE for car {vin}",
                $"Could not get body controller state: {bodyControllerStateResult.ResultMessage}",
                issueKeys.BleDataCollectionError, vin, null).ConfigureAwait(false);
            return;
        }

        var bodyControllerState = DeserializeBodyControllerState(bodyControllerStateResult.ResultMessage);
        if (bodyControllerState == default)
        {
            logger.LogError("Could not parse body controller state for car {vin}: {resultMessage}", vin, bodyControllerStateResult.ResultMessage);
            await errorHandlingService.HandleError(nameof(BleVehicleDataService), nameof(RefreshPresentCarData),
                $"Error while getting vehicle data via BLE for car {vin}",
                $"Could not parse body controller state: {bodyControllerStateResult.ResultMessage}",
                issueKeys.BleDataCollectionError, vin, null).ConfigureAwait(false);
            return;
        }

        var isAwake = string.Equals(bodyControllerState.VehicleSleepStatus, AwakeSleepStatus, StringComparison.OrdinalIgnoreCase);
        UpdateOnlineState(car, isAwake, timestamp);
        if (!isAwake)
        {
            //Do not get the charge state of sleeping cars as requests to the infotainment system would wake up the
            //car. The last known charge state values stay valid until the car is awake again.
            await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
            await errorHandlingService.HandleErrorResolved(issueKeys.BleDataCollectionError, vin).ConfigureAwait(false);
            return;
        }

        //Note: Requests to the infotainment system reset the car's standby timer. Based on current knowledge the car
        //still falls asleep even when polled frequently. If real world tests show that cars do not fall asleep because
        //of this polling, implement a sleep policy here: e.g. stop polling the charge state after several minutes
        //without plugged in or charging state changes while the car is unplugged, so the standby timer can run out.
        var chargeStateResult = await bleService.GetChargeState(vin).ConfigureAwait(false);
        if (!chargeStateResult.Success)
        {
            logger.LogError("Could not get charge state for car {vin}: {outcome} {resultMessage}", vin,
                chargeStateResult.Outcome, chargeStateResult.ResultMessage);
            await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
            await errorHandlingService.HandleError(nameof(BleVehicleDataService), nameof(RefreshPresentCarData),
                $"Error while getting vehicle data via BLE for car {vin}",
                $"Could not get charge state: {chargeStateResult.ResultMessage}",
                issueKeys.BleDataCollectionError, vin, null).ConfigureAwait(false);
            return;
        }
        var chargeState = DeserializeChargeState(chargeStateResult.ResultMessage);
        if (chargeState == default)
        {
            logger.LogError("Could not parse charge state for car {vin}: {resultMessage}", vin, chargeStateResult.ResultMessage);
            await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
            await errorHandlingService.HandleError(nameof(BleVehicleDataService), nameof(RefreshPresentCarData),
                $"Error while getting vehicle data via BLE for car {vin}",
                $"Could not parse charge state: {chargeStateResult.ResultMessage}",
                issueKeys.BleDataCollectionError, vin, null).ConfigureAwait(false);
            return;
        }
        UpdateChargeStateValues(car, chargeState, timestamp);
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        await errorHandlingService.HandleErrorResolved(issueKeys.BleDataCollectionError, vin).ConfigureAwait(false);
    }

    internal void UpdateChargeStateValues(DtoCar car, DtoBleChargeState chargeState, DateTime timestamp)
    {
        AddIntValue(car, CarValueType.StateOfCharge, chargeState.BatteryLevel, timestamp);
        AddIntValue(car, CarValueType.StateOfChargeLimit, chargeState.ChargeLimitSoc, timestamp);
        AddIntValue(car, CarValueType.ChargerVoltage, chargeState.ChargerVoltage, timestamp);
        AddIntValue(car, CarValueType.ChargeAmps, chargeState.ChargerActualCurrent, timestamp);
        AddIntValue(car, CarValueType.ChargerPhases, chargeState.ChargerPhases, timestamp);
        AddIntValue(car, CarValueType.ChargeCurrentRequest, chargeState.ChargeCurrentRequest, timestamp);
        AddIntValue(car, CarValueType.ChargerPilotCurrent, chargeState.ChargerPilotCurrent, timestamp);
        var chargingStateName = GetChargingStateName(chargeState.ChargingState);
        if ((chargingStateName != default)
            && !string.Equals(chargingStateName, ChargingStateUnknown, StringComparison.OrdinalIgnoreCase))
        {
            AddBooleanValue(car, CarValueType.IsPluggedIn,
                !string.Equals(chargingStateName, ChargingStateDisconnected, StringComparison.OrdinalIgnoreCase), timestamp);
            AddBooleanValue(car, CarValueType.IsCharging,
                string.Equals(chargingStateName, ChargingStateCharging, StringComparison.OrdinalIgnoreCase), timestamp);
        }
    }

    private void UpdateHomePresence(DtoCar car, bool isAtHome, DateTime timestamp)
    {
        AddBooleanValue(car, CarValueType.LocatedAtHome, isAtHome, timestamp);
        car.IsHomeGeofence.Update(new DateTimeOffset(timestamp, TimeSpan.Zero), isAtHome);
    }

    private void UpdateOnlineState(DtoCar car, bool isOnline, DateTime timestamp)
    {
        AddBooleanValue(car, CarValueType.AsleepOrOffline, !isOnline, timestamp);
        car.IsOnline.Update(new DateTimeOffset(timestamp, TimeSpan.Zero), isOnline);
    }

    private void AddIntValue(DtoCar car, CarValueType type, int? value, DateTime timestamp,
        CarValueSource source = CarValueSource.Ble, bool skipDefaultValue = true)
    {
        if (skipDefaultValue && value == default)
        {
            return;
        }
        var carValueLog = new CarValueLog
        {
            CarId = car.Id,
            Timestamp = timestamp,
            Type = type,
            Source = source,
            IntValue = value,
        };
        teslaSolarChargerContext.CarValueLogs.Add(carValueLog);
        carPropertyUpdateHelper.UpdateDtoCarProperty(car, carValueLog);
    }

    private void AddBooleanValue(DtoCar car, CarValueType type, bool value, DateTime timestamp,
        CarValueSource source = CarValueSource.Ble)
    {
        var carValueLog = new CarValueLog
        {
            CarId = car.Id,
            Timestamp = timestamp,
            Type = type,
            Source = source,
            BooleanValue = value,
        };
        teslaSolarChargerContext.CarValueLogs.Add(carValueLog);
        carPropertyUpdateHelper.UpdateDtoCarProperty(car, carValueLog);
    }

    internal static DtoBleBodyControllerState? DeserializeBodyControllerState(string? resultMessage)
    {
        if (string.IsNullOrWhiteSpace(resultMessage))
        {
            return default;
        }
        try
        {
            return JsonConvert.DeserializeObject<DtoBleBodyControllerState>(resultMessage);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    internal static DtoBleChargeState? DeserializeChargeState(string? resultMessage)
    {
        if (string.IsNullOrWhiteSpace(resultMessage))
        {
            return default;
        }
        try
        {
            var vehicleData = JsonConvert.DeserializeObject<DtoBleVehicleData>(resultMessage);
            if (vehicleData?.ChargeState != default)
            {
                return vehicleData.ChargeState;
            }
            //Fallback in case the CLI output is not wrapped in a VehicleData message
            return JsonConvert.DeserializeObject<DtoBleChargeState>(resultMessage);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    internal static string? GetChargingStateName(JToken? chargingState)
    {
        return chargingState switch
        {
            null => default,
            //In the protobuf definition the charging state is a oneof of empty messages, so protojson serializes it
            //as an object with a single property, e.g. {"Charging": {}}.
            JObject chargingStateObject => chargingStateObject.Properties().FirstOrDefault()?.Name,
            JValue { Type: JTokenType.String } chargingStateValue => chargingStateValue.Value<string>(),
            _ => default,
        };
    }
}
