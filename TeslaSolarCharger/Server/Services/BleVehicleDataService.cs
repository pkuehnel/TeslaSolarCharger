using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos.Ble;
using TeslaSolarCharger.Server.Helper.Contracts;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Ble;
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
    IBleReadCoordinator bleReadCoordinator,
    IBleSleepWindowService bleSleepWindowService,
    IBlePresenceStateService blePresenceStateService,
    IIssueKeys issueKeys) : IBleVehicleDataService
{
    private const string AwakeSleepStatus = "VEHICLE_SLEEP_STATUS_AWAKE";
    private const string ChargingStateDisconnected = "Disconnected";
    private const string ChargingStateCharging = "Charging";
    private const string ChargingStateUnknown = "Unknown";

    public async Task RefreshBleCarData()
    {
        logger.LogTrace("{method}()", nameof(RefreshBleCarData));
        if (!configurationWrapper.GetVehicleDataViaBle() || !configurationWrapper.GetVehicleDataFromTesla())
        {
            //No car is BLE polled anymore: drop all presence state so no car keeps a stale uncertain presence that
            //would suppress its charging commands forever.
            blePresenceStateService.RetainOnly(Array.Empty<int>());
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
        //Cars that left BLE data collection mode must not keep a stale presence state (see RetainOnly docs).
        blePresenceStateService.RetainOnly(bleDataCarIds);
        foreach (var carId in bleDataCarIds)
        {
            var car = settings.Cars.FirstOrDefault(c => c.Id == carId);
            if (car == default || string.IsNullOrEmpty(car.Vin))
            {
                continue;
            }
            await RefreshCarDataGuarded(car).ConfigureAwait(false);
        }
    }

    public async Task RefreshSingleCarData(int carId)
    {
        logger.LogTrace("{method}({carId})", nameof(RefreshSingleCarData), carId);
        if (!configurationWrapper.GetVehicleDataViaBle() || !configurationWrapper.GetVehicleDataFromTesla())
        {
            return;
        }
        //Apply the same filter as RefreshBleCarData so only cars that actually collect their data via BLE are refreshed.
        var isBleDataCar = await teslaSolarChargerContext.Cars
            .Where(c => (c.Id == carId)
                        && (c.ShouldBeManaged == true)
                        && (c.CarType == CarType.Tesla)
                        && c.UseBle
                        && !c.UseFleetTelemetry
                        && !c.IncludeTrackingRelevantFields)
            .AnyAsync().ConfigureAwait(false);
        if (!isBleDataCar)
        {
            logger.LogDebug("Car {carId} does not collect its data via BLE, skip delayed BLE refresh", carId);
            return;
        }
        var car = settings.Cars.FirstOrDefault(c => c.Id == carId);
        if (car == default || string.IsNullOrEmpty(car.Vin))
        {
            return;
        }
        await RefreshCarDataGuarded(car).ConfigureAwait(false);
    }

    private async Task RefreshCarDataGuarded(DtoCar car)
    {
        //Never read a single car via BLE from two places at once: the regular cycle read and the delayed post command
        //read would otherwise hit the same BLE container simultaneously. If a read for this car is already running,
        //skip this one.
        if (!bleReadCoordinator.TryBeginRead(car.Id))
        {
            logger.LogDebug("Skip BLE read for car {vin} as another read is already in progress", car.Vin);
            return;
        }
        try
        {
            await RefreshCarData(car).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while refreshing BLE data for car {vin}", car.Vin);
            await errorHandlingService.HandleError(nameof(BleVehicleDataService), nameof(RefreshBleCarData),
                $"Error while getting vehicle data via BLE for car {car.Vin}", ex.Message,
                issueKeys.BleDataCollectionError, car.Vin, ex.StackTrace).ConfigureAwait(false);
        }
        finally
        {
            bleReadCoordinator.EndRead(car.Id);
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

    private async Task RefreshCarData(DtoCar car)
    {
        logger.LogTrace("{method}({vin})", nameof(RefreshCarData), car.Vin);
        if (car.IsCharging.Value == true)
        {
            //A charging car is definitely awake and not trying to sleep, so it can never be in a sleep window. Reset any
            //state so a fresh stability period starts once charging stops.
            bleSleepWindowService.ResetSleepWindow(car.Id);
            //While the car is charging it is known to be online and at home, so the VCSEC body controller state call is
            //not required. Reading only the charge state roughly halves the time a refresh takes for a charging car.
            if (await TryRefreshChargingCarData(car).ConfigureAwait(false))
            {
                return;
            }
            //The charge state read failed because the car is out of BLE range: the last known charging state was stale
            //(e.g. the car was unplugged and driven away). Fall back to the body controller state to correctly
            //determine the presence and online state.
            logger.LogDebug("Fast charge state read for car {vin} failed as out of BLE range, fall back to body controller state", car.Vin);
        }
        await RefreshCarDataViaBodyController(car).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads only the charge state (infotainment) of a charging car, skipping the VCSEC body controller call.
    /// </summary>
    /// <returns>
    /// True if the refresh was handled (charge state stored or a non range error raised). False only if the car turned
    /// out to be out of BLE range, so the caller should fall back to the body controller state.
    /// </returns>
    private async Task<bool> TryRefreshChargingCarData(DtoCar car)
    {
        var vin = car.Vin!;
        var chargeStateResult = await bleService.GetChargeState(vin).ConfigureAwait(false);
        var timestamp = dateTimeProvider.UtcNow();
        if (!chargeStateResult.Success)
        {
            if (IsCarOutOfBleRangeResult(chargeStateResult))
            {
                return false;
            }
            logger.LogError("Could not get charge state for charging car {vin}: {resultMessage}", vin, chargeStateResult.ResultMessage);
            await errorHandlingService.HandleError(nameof(BleVehicleDataService), nameof(TryRefreshChargingCarData),
                $"Error while getting vehicle data via BLE for car {vin}",
                $"Could not get charge state: {chargeStateResult.ResultMessage}",
                issueKeys.BleDataCollectionError, vin, null).ConfigureAwait(false);
            return true;
        }
        //The car answered the request, so it is in BLE range even if the response turns out to be unparseable.
        blePresenceStateService.RegisterSuccessfulRead(car.Id);
        var chargeState = DeserializeChargeState(chargeStateResult.ResultMessage);
        if (chargeState == default)
        {
            logger.LogError("Could not parse charge state for charging car {vin}: {resultMessage}", vin, chargeStateResult.ResultMessage);
            await errorHandlingService.HandleError(nameof(BleVehicleDataService), nameof(TryRefreshChargingCarData),
                $"Error while getting vehicle data via BLE for car {vin}",
                $"Could not parse charge state: {chargeStateResult.ResultMessage}",
                issueKeys.BleDataCollectionError, vin, null).ConfigureAwait(false);
            return true;
        }
        //The car answered the charge state request, so it is in BLE range (at home) and awake (online).
        UpdateHomePresence(car, true, timestamp);
        UpdateOnlineState(car, true, timestamp);
        UpdateChargeStateValues(car, chargeState, timestamp);
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        await errorHandlingService.HandleErrorResolved(issueKeys.BleDataCollectionError, vin).ConfigureAwait(false);
        return true;
    }

    private async Task RefreshCarDataViaBodyController(DtoCar car)
    {
        logger.LogTrace("{method}({vin})", nameof(RefreshCarDataViaBodyController), car.Vin);
        var vin = car.Vin!;
        var bodyControllerStateResult = await bleService.GetBodyControllerState(vin).ConfigureAwait(false);
        var timestamp = dateTimeProvider.UtcNow();
        if (!bodyControllerStateResult.Success)
        {
            if (IsCarOutOfBleRangeResult(bodyControllerStateResult))
            {
                //An out of range result can also be a transient BLE stack failure while the car is at home, so the
                //car is only confirmed as away after multiple consecutive out of range results. Until then the last
                //known state stays valid but new charging commands are suspended (see IsPresenceUncertain callers).
                var awayConfirmation = blePresenceStateService.RegisterOutOfRange(car.Id);
                if (awayConfirmation == BleAwayConfirmation.NotConfirmed)
                {
                    logger.LogDebug("BLE beacon for car {vin} not found, keeping last known state until the car is " +
                                    "confirmed as away", vin);
                    return;
                }
                if (awayConfirmation == BleAwayConfirmation.AlreadyConfirmed)
                {
                    //The car is already marked as away: nothing changed, so do not write the same values again.
                    return;
                }
                //The car is now confirmed out of BLE range: it is not at home and as it is not reachable it counts as
                //offline. The charge port can not be plugged in anymore, so reset the stale charging values too.
                logger.LogDebug("BLE beacon for car {vin} not found multiple times in a row, car is not at home", vin);
                UpdateHomePresence(car, false, timestamp);
                UpdateOnlineState(car, false, timestamp);
                ResetChargingValuesForAwayCar(car, timestamp);
                //The car left home: drop any sleep window so it restarts fresh when it comes back.
                bleSleepWindowService.ResetSleepWindow(car.Id);
                await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
                await errorHandlingService.HandleErrorResolved(issueKeys.BleDataCollectionError, vin).ConfigureAwait(false);
                return;
            }
            //Other errors (e.g. BLE container not reachable, semaphore timeouts, configuration issues) carry no
            //information about the car's presence, so they neither increment nor reset the out of range counter.
            logger.LogError("Could not get body controller state for car {vin}: {resultMessage}", vin, bodyControllerStateResult.ResultMessage);
            await errorHandlingService.HandleError(nameof(BleVehicleDataService), nameof(RefreshCarData),
                $"Error while getting vehicle data via BLE for car {vin}",
                $"Could not get body controller state: {bodyControllerStateResult.ResultMessage}",
                issueKeys.BleDataCollectionError, vin, null).ConfigureAwait(false);
            return;
        }
        //The car answered the request, so it is in BLE range even if the response turns out to be unparseable.
        blePresenceStateService.RegisterSuccessfulRead(car.Id);

        var bodyControllerState = DeserializeBodyControllerState(bodyControllerStateResult.ResultMessage);
        if (bodyControllerState == default)
        {
            logger.LogError("Could not parse body controller state for car {vin}: {resultMessage}", vin, bodyControllerStateResult.ResultMessage);
            await errorHandlingService.HandleError(nameof(BleVehicleDataService), nameof(RefreshCarData),
                $"Error while getting vehicle data via BLE for car {vin}",
                $"Could not parse body controller state: {bodyControllerStateResult.ResultMessage}",
                issueKeys.BleDataCollectionError, vin, null).ConfigureAwait(false);
            return;
        }

        //The body controller responded, so the car is in BLE range and therefore at home.
        UpdateHomePresence(car, true, timestamp);
        var isAwake = string.Equals(bodyControllerState.VehicleSleepStatus, AwakeSleepStatus, StringComparison.OrdinalIgnoreCase);
        UpdateOnlineState(car, isAwake, timestamp);
        if (!isAwake)
        {
            //Do not get the charge state of sleeping cars as requests to the infotainment system would wake up the
            //car. The last known charge state values stay valid until the car is awake again.
            //The car reached sleep: keep the state (so the UI can show it) but mark it asleep. The next time it wakes a
            //fresh stability period starts.
            bleSleepWindowService.NotifyAsleep(car.Id);
            await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
            await errorHandlingService.HandleErrorResolved(issueKeys.BleDataCollectionError, vin).ConfigureAwait(false);
            return;
        }

        //Requests to the infotainment system reset the car's standby timer and keep it awake (verified on a real car:
        //polling the charge state prevents sleep, the VCSEC body controller poll does not). While an idle car is in a
        //BLE sleep window the infotainment poll is therefore withheld so the standby timer can run out.
        var windowMinutes = configurationWrapper.BleSleepWindowMinutes();
        var stabilityMinutes = configurationWrapper.BleSleepStabilityMinutes();
        if (!bleSleepWindowService.ShouldPollInfotainment(car.Id, timestamp, windowMinutes))
        {
            logger.LogDebug("Car {vin} is in a BLE sleep window, skip infotainment charge state poll", vin);
            await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
            await errorHandlingService.HandleErrorResolved(issueKeys.BleDataCollectionError, vin).ConfigureAwait(false);
            return;
        }
        var chargeStateResult = await bleService.GetChargeState(vin).ConfigureAwait(false);
        if (!chargeStateResult.Success)
        {
            logger.LogError("Could not get charge state for car {vin}: {resultMessage}", vin, chargeStateResult.ResultMessage);
            await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
            await errorHandlingService.HandleError(nameof(BleVehicleDataService), nameof(RefreshCarData),
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
            await errorHandlingService.HandleError(nameof(BleVehicleDataService), nameof(RefreshCarData),
                $"Error while getting vehicle data via BLE for car {vin}",
                $"Could not parse charge state: {chargeStateResult.ResultMessage}",
                issueKeys.BleDataCollectionError, vin, null).ConfigureAwait(false);
            return;
        }
        UpdateChargeStateValues(car, chargeState, timestamp);
        //Feed the fresh full poll into the sleep window state machine so it can (re-)start a window once the car has
        //been idle and closed up long enough.
        bleSleepWindowService.ObserveFullPoll(car.Id, bodyControllerState, DerivePluggedIn(chargeState),
            chargeState.ChargeLimitSoc, timestamp, windowMinutes, stabilityMinutes);
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        await errorHandlingService.HandleErrorResolved(issueKeys.BleDataCollectionError, vin).ConfigureAwait(false);
    }

    /// <summary>
    /// Derives the plugged in state from the BLE charging state, or null if it is unknown/not reported.
    /// </summary>
    internal static bool? DerivePluggedIn(DtoBleChargeState chargeState)
    {
        var chargingStateName = GetChargingStateName(chargeState.ChargingState);
        if (chargingStateName == default
            || string.Equals(chargingStateName, ChargingStateUnknown, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return !string.Equals(chargingStateName, ChargingStateDisconnected, StringComparison.OrdinalIgnoreCase);
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

    /// <summary>
    /// A car that is confirmed as away can not be plugged in at home anymore: reset the charging values that would
    /// otherwise stay stale until the car is back in BLE range and awake. The state of charge intentionally keeps its
    /// last known value. The values are inferred rather than read from the car, so they are stored with
    /// <see cref="CarValueSource.Estimation"/>.
    /// </summary>
    private void ResetChargingValuesForAwayCar(DtoCar car, DateTime timestamp)
    {
        AddBooleanValue(car, CarValueType.IsPluggedIn, false, timestamp, CarValueSource.Estimation);
        AddBooleanValue(car, CarValueType.IsCharging, false, timestamp, CarValueSource.Estimation);
        AddIntValue(car, CarValueType.ChargeAmps, 0, timestamp, CarValueSource.Estimation);
    }

    private void AddIntValue(DtoCar car, CarValueType type, int? value, DateTime timestamp,
        CarValueSource source = CarValueSource.Ble)
    {
        if (value == default)
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

    internal static bool IsCarOutOfBleRangeResult(DtoBleCommandResult result)
    {
        if (result.ResultMessage == default)
        {
            return false;
        }
        //Depending on the tesla-control version the BLE beacon scan for a car that is not in range fails with
        //"failed to find BLE beacon for <vin>" or just with "Error: context deadline exceeded" (verified against a real
        //BLE container on 2026-07-19). A present car answers the scan within a few seconds, so a scan timeout means
        //the car is (very likely) not in BLE range.
        return result.ResultMessage.Contains("beacon", StringComparison.OrdinalIgnoreCase)
               || result.ResultMessage.Contains("context deadline exceeded", StringComparison.OrdinalIgnoreCase);
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
