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
        foreach (var carId in bleDataCarIds)
        {
            var car = settings.Cars.FirstOrDefault(c => c.Id == carId);
            if (car == default || string.IsNullOrEmpty(car.Vin))
            {
                continue;
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

    private async Task RefreshCarData(DtoCar car)
    {
        logger.LogTrace("{method}({vin})", nameof(RefreshCarData), car.Vin);
        var vin = car.Vin!;
        var bodyControllerStateResult = await bleService.GetBodyControllerState(vin).ConfigureAwait(false);
        var timestamp = dateTimeProvider.UtcNow();
        if (!bodyControllerStateResult.Success)
        {
            if (IsBeaconNotFoundResult(bodyControllerStateResult))
            {
                //The car is out of BLE range: it is not at home and as it is not reachable it counts as offline.
                logger.LogDebug("BLE beacon for car {vin} not found, car is not at home", vin);
                UpdateHomePresence(car, false, timestamp);
                UpdateOnlineState(car, false, timestamp);
                await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
                await errorHandlingService.HandleErrorResolved(issueKeys.BleDataCollectionError, vin).ConfigureAwait(false);
                return;
            }
            logger.LogError("Could not get body controller state for car {vin}: {resultMessage}", vin, bodyControllerStateResult.ResultMessage);
            await errorHandlingService.HandleError(nameof(BleVehicleDataService), nameof(RefreshCarData),
                $"Error while getting vehicle data via BLE for car {vin}",
                $"Could not get body controller state: {bodyControllerStateResult.ResultMessage}",
                issueKeys.BleDataCollectionError, vin, null).ConfigureAwait(false);
            return;
        }

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

    private void AddIntValue(DtoCar car, CarValueType type, int? value, DateTime timestamp)
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
            Source = CarValueSource.Ble,
            IntValue = value,
        };
        teslaSolarChargerContext.CarValueLogs.Add(carValueLog);
        carPropertyUpdateHelper.UpdateDtoCarProperty(car, carValueLog);
    }

    private void AddBooleanValue(DtoCar car, CarValueType type, bool value, DateTime timestamp)
    {
        var carValueLog = new CarValueLog
        {
            CarId = car.Id,
            Timestamp = timestamp,
            Type = type,
            Source = CarValueSource.Ble,
            BooleanValue = value,
        };
        teslaSolarChargerContext.CarValueLogs.Add(carValueLog);
        carPropertyUpdateHelper.UpdateDtoCarProperty(car, carValueLog);
    }

    internal static bool IsBeaconNotFoundResult(DtoBleCommandResult result)
    {
        //tesla-control fails with "failed to find BLE beacon for <vin>" when the car is not in BLE range.
        return result.ResultMessage?.Contains("beacon", StringComparison.OrdinalIgnoreCase) == true;
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
