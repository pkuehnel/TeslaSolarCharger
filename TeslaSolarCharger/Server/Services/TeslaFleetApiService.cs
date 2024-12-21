using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http.Headers;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.TeslaFleetApi;
using TeslaSolarCharger.Server.Dtos.TscBackend;
using TeslaSolarCharger.Server.Resources.PossibleIssues.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Ble;
using TeslaSolarCharger.Shared.Dtos.Car;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedBackend.Dtos;
using TeslaSolarCharger.SharedBackend.Enums;
using Error = LanguageExt.Common.Error;

namespace TeslaSolarCharger.Server.Services;

public class TeslaFleetApiService(
    ILogger<TeslaFleetApiService> logger,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    IDateTimeProvider dateTimeProvider,
    IConfigurationWrapper configurationWrapper,
    IConstants constants,
    ITscConfigurationService tscConfigurationService,
    IErrorHandlingService errorHandlingService,
    ISettings settings,
    IBleService bleService,
    IIssueKeys issueKeys,
    ITeslaFleetApiTokenHelper teslaFleetApiTokenHelper,
    IFleetTelemetryWebSocketService fleetTelemetryWebSocketService)
    : ITeslaService, ITeslaFleetApiService
{
    private const string IsChargingErrorMessage = "is_charging";
    private const string IsNotChargingErrorMessage = "not_charging";

    private DtoFleetApiRequest ChargeStartRequest => new()
    {
        RequestUrl = constants.ChargeStartRequestUrl,
        NeedsProxy = true,
        BleCompatible = true,
        TeslaApiRequestType = TeslaApiRequestType.Charging,
    };
    private DtoFleetApiRequest ChargeStopRequest => new()
    {
        RequestUrl = constants.ChargeStopRequestUrl,
        NeedsProxy = true,
        BleCompatible = true,
        TeslaApiRequestType = TeslaApiRequestType.Charging,
    };
    private DtoFleetApiRequest SetChargingAmpsRequest => new()
    {
        RequestUrl = constants.SetChargingAmpsRequestUrl,
        NeedsProxy = true,
        BleCompatible = true,
        TeslaApiRequestType = TeslaApiRequestType.Command,
    };
    private DtoFleetApiRequest SetScheduledChargingRequest => new()
    {
        RequestUrl = constants.SetScheduledChargingRequestUrl,
        NeedsProxy = true,
        TeslaApiRequestType = TeslaApiRequestType.Command,
    };
    private DtoFleetApiRequest SetChargeLimitRequest => new()
    {
        RequestUrl = constants.SetChargeLimitRequestUrl,
        NeedsProxy = true,
        TeslaApiRequestType = TeslaApiRequestType.Command,
    };
    private DtoFleetApiRequest SetSentryModeRequest => new()
    {
        RequestUrl = constants.SetSentryModeRequestUrl,
        NeedsProxy = true,
        TeslaApiRequestType = TeslaApiRequestType.Command,
    };
    private DtoFleetApiRequest FlashHeadlightsRequest => new()
    {
        RequestUrl = constants.FlashHeadlightsRequestUrl,
        NeedsProxy = true,
        //Do not make this BLE compatible as this is used to test fleet api access
        BleCompatible = false,
        TeslaApiRequestType = TeslaApiRequestType.Charging,
    };
    private DtoFleetApiRequest WakeUpRequest => new()
    {
        RequestUrl = constants.WakeUpRequestUrl,
        NeedsProxy = false,
        TeslaApiRequestType = TeslaApiRequestType.WakeUp,
        BleCompatible = true,
    };

    private DtoFleetApiRequest VehicleRequest => new()
    {
        RequestUrl = constants.VehicleRequestUrl,
        NeedsProxy = false,
        TeslaApiRequestType = TeslaApiRequestType.Vehicle,
    };

    private DtoFleetApiRequest VehicleDataRequest => new()
    {
        RequestUrl = constants.VehicleDataRequestUrl,
        NeedsProxy = false,
        TeslaApiRequestType = TeslaApiRequestType.VehicleData,
    };

    public async Task StartCharging(int carId, int startAmp, CarStateEnum? carState)
    {
        logger.LogTrace("{method}({carId}, {startAmp}, {carState})", nameof(StartCharging), carId, startAmp, carState);
        var car = settings.Cars.First(c => c.Id == carId);
        if (car.ChargeStartCalls.OrderDescending().FirstOrDefault() > (dateTimeProvider.UtcNow() + TimeSpan.FromMinutes(1)))
        {
            logger.LogDebug("Last charge start call is less than a minute old. Do not send command again.");
            return;
        }
        if (startAmp == 0)
        {
            logger.LogDebug("Should start charging with 0 amp. Skipping charge start.");
            return;
        }
        await WakeUpCarIfNeeded(carId, carState).ConfigureAwait(false);

        var vin = GetVinByCarId(carId);
        await SetAmp(carId, startAmp).ConfigureAwait(false);

        var result = await SendCommandToTeslaApi<DtoVehicleCommandResult>(vin, ChargeStartRequest, HttpMethod.Post).ConfigureAwait(false);
    }


    public async Task WakeUpCar(int carId)
    {
        logger.LogTrace("{method}({carId})", nameof(WakeUpCar), carId);
        var car = settings.Cars.First(c => c.Id == carId);
        var result = await SendCommandToTeslaApi<DtoVehicleWakeUpResult>(car.Vin, WakeUpRequest, HttpMethod.Post).ConfigureAwait(false);
        if (car.TeslaMateCarId != default)
        {
            //ToDo: fix with https://github.com/pkuehnel/TeslaSolarCharger/issues/1511
            //await teslamateApiService.ResumeLogging(car.TeslaMateCarId.Value).ConfigureAwait(false);
        }
        await Task.Delay(TimeSpan.FromSeconds(20)).ConfigureAwait(false);
    }

    public async Task StopCharging(int carId)
    {
        logger.LogTrace("{method}({carId})", nameof(StopCharging), carId);
        var car = settings.Cars.First(c => c.Id == carId);
        if (car.ChargeStopCalls.OrderDescending().FirstOrDefault() > (dateTimeProvider.UtcNow() + TimeSpan.FromMinutes(1)))
        {
            logger.LogDebug("Last charge stop call is less than a minute old. Do not send command again.");
            return;
        }
        var vin = GetVinByCarId(carId);
        
        var result = await SendCommandToTeslaApi<DtoVehicleCommandResult>(vin, ChargeStopRequest, HttpMethod.Post).ConfigureAwait(false);
    }

    public async Task SetAmp(int carId, int amps)
    {
        logger.LogTrace("{method}({carId}, {amps})", nameof(SetAmp), carId, amps);
        var car = settings.Cars.First(c => c.Id == carId);
        if (car.ChargerRequestedCurrent == amps)
        {
            logger.LogDebug("Correct charging amp already set.");
            return;
        }
        var vin = GetVinByCarId(carId);
        var commandData = $"{{\"charging_amps\":{amps}}}";
        var result = await SendCommandToTeslaApi<DtoVehicleCommandResult>(vin, SetChargingAmpsRequest, HttpMethod.Post, commandData, amps).ConfigureAwait(false);
        car.LastSetAmp = amps;
    }

    public async Task SetScheduledCharging(int carId, DateTimeOffset? chargingStartTime)
    {
        logger.LogTrace("{method}({param1}, {param2})", nameof(SetScheduledCharging), carId, chargingStartTime);
        var vin = GetVinByCarId(carId);
        var car = settings.Cars.First(c => c.Id == carId);
        if (!IsChargingScheduleChangeNeeded(chargingStartTime, dateTimeProvider.DateTimeOffSetNow(), car, out var parameters))
        {
            logger.LogDebug("No change in updating scheduled charging needed.");
            return;
        }

        await WakeUpCarIfNeeded(carId, car.State).ConfigureAwait(false);

        var result = await SendCommandToTeslaApi<DtoVehicleCommandResult>(vin, SetScheduledChargingRequest, HttpMethod.Post, JsonConvert.SerializeObject(parameters)).ConfigureAwait(false);
        //assume update was sucessfull as update is not working after mosquitto restart (or wrong cached State)
        if (parameters["enable"] == "false")
        {
            car.ScheduledChargingStartTime = null;
        }
    }

    public async Task SetChargeLimit(int carId, int limitSoC)
    {
        logger.LogTrace("{method}({param1}, {param2})", nameof(SetChargeLimit), carId, limitSoC);
        var vin = GetVinByCarId(carId);
        var car = settings.Cars.First(c => c.Id == carId);
        await WakeUpCarIfNeeded(carId, car.State).ConfigureAwait(false);
        var parameters = new Dictionary<string, int>()
        {
            { "percent", limitSoC },
        };
        await SendCommandToTeslaApi<DtoVehicleCommandResult>(vin, SetChargeLimitRequest, HttpMethod.Post, JsonConvert.SerializeObject(parameters)).ConfigureAwait(false);
    }

    public async Task SetSentryMode(int carId, bool active)
    {
        logger.LogTrace("{method}({param1}, {param2})", nameof(SetSentryMode), carId, active);
        var vin = GetVinByCarId(carId);
        var car = settings.Cars.First(c => c.Id == carId);
        await WakeUpCarIfNeeded(carId, car.State).ConfigureAwait(false);
        var parameters = new Dictionary<string, int>()
        {
            { "on", active ? 1 : 0 },
        };
        await SendCommandToTeslaApi<DtoVehicleCommandResult>(vin, SetSentryModeRequest, HttpMethod.Post, JsonConvert.SerializeObject(parameters)).ConfigureAwait(false);
    }

    public async Task<DtoValue<bool>> TestFleetApiAccess(int carId)
    {
        logger.LogTrace("{method}({carId})", nameof(TestFleetApiAccess), carId);
        var vin = GetVinByCarId(carId);
        var inMemoryCar = settings.Cars.First(c => c.Id == carId);
        try
        {
            await WakeUpCarIfNeeded(carId, inMemoryCar.State).ConfigureAwait(false);
            var result = await SendCommandToTeslaApi<DtoVehicleCommandResult>(vin, FlashHeadlightsRequest, HttpMethod.Post).ConfigureAwait(false);
            var successResult = result?.Response?.Result == true;
            var car = teslaSolarChargerContext.Cars.First(c => c.Id == carId);
            car.TeslaFleetApiState = successResult ? TeslaCarFleetApiState.Ok : TeslaCarFleetApiState.NotWorking;
            await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
            return new DtoValue<bool>(successResult);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Testing fleet api access was not successfull");
            return new DtoValue<bool>(false);
        }
    }

    public async Task<DtoValue<bool>> IsFleetApiProxyEnabled(string vin)
    {
        logger.LogTrace("{method}", nameof(IsFleetApiProxyEnabled));
        var fleetApiProxyEnabled = await teslaSolarChargerContext.Cars
            .Where(c => c.Vin == vin)
            .Select(c => c.VehicleCommandProtocolRequired)
            .FirstAsync();
        return new DtoValue<bool>(fleetApiProxyEnabled);
    }

    public async Task RefreshCarData()
    {
        logger.LogTrace("{method}()", nameof(RefreshCarData));
        if ((!configurationWrapper.GetVehicleDataFromTesla()))
        {
            logger.LogDebug("Vehicle Data are coming from TeslaMate. Do not refresh car states via Fleet API");
            return;
        }
        var carIds = settings.CarsToManage.Select(c => c.Id).ToList();
        foreach (var carId in carIds)
        {
            var car = settings.Cars.First(c => c.Id == carId);
            if (!(await IsCarDataRefreshNeeded(car)))
            {
                logger.LogDebug("Do not refresh car data for car {carId} to prevent rate limits", car.Id);
                continue;
            }
            try
            {
                var vehicle = await SendCommandToTeslaApi<DtoVehicleResult>(car.Vin, VehicleRequest, HttpMethod.Get).ConfigureAwait(false);
                var vehicleResult = vehicle?.Response;
                logger.LogTrace("Got vehicle {@vehicle}", vehicle);
                if (vehicleResult == default)
                {
                    await errorHandlingService.HandleError(nameof(TeslaFleetApiService), nameof(RefreshCarData), $"Error while getting vehicle info for car {car.Vin}",
                                               $"Could not deserialize vehicle: {JsonConvert.SerializeObject(vehicle)}", issueKeys.GetVehicle, car.Vin, null).ConfigureAwait(false);
                    logger.LogError("Could not deserialize vehicle for car {carId}: {@vehicle}", carId, vehicle);
                    continue;
                }
                await errorHandlingService.HandleErrorResolved(issueKeys.GetVehicle, car.Vin);
                var vehicleState = vehicleResult.State;
                if (configurationWrapper.GetVehicleDataFromTesla())
                {
                    var carStateLog = new CarValueLog()
                    {
                        CarId = car.Id,
                        Timestamp = dateTimeProvider.UtcNow(),
                        Source = CarValueSource.FleetApi,
                        Type = CarValueType.AsleepOrOffline,
                    };
                    if (vehicleState == "asleep")
                    {
                        carStateLog.BooleanValue = true;
                        car.State = CarStateEnum.Asleep;
                    }
                    else if (vehicleState == "offline")
                    {
                        carStateLog.BooleanValue = true;
                        car.State = CarStateEnum.Offline;
                    }
                    else
                    {
                        carStateLog.BooleanValue = false;
                    }
                    teslaSolarChargerContext.CarValueLogs.Add(carStateLog);
                    await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
                }

                if (vehicleState is "asleep" or "offline")
                {
                    logger.LogDebug("Do not call current vehicle data as car is {state}", vehicleState);
                    continue;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not get vehicle data for car {carId}", carId);
                await errorHandlingService.HandleError(nameof(TeslaFleetApiService), nameof(RefreshCarData), $"Error while refreshing car data for car {car.Vin}",
                    $"Error getting vehicle data: {ex.Message} {ex.StackTrace}", issueKeys.GetVehicle, car.Vin, ex.StackTrace).ConfigureAwait(false);
            }

            try
            {
                var vehicleData = await SendCommandToTeslaApi<DtoVehicleDataResult>(car.Vin, VehicleDataRequest, HttpMethod.Get)
                    .ConfigureAwait(false);
                logger.LogTrace("Got vehicleData {@vehicleData}", vehicleData);
                var vehicleDataResult = vehicleData?.Response;
                if (vehicleData?.Error?.Contains("offline") == true)
                {
                    car.State = CarStateEnum.Offline;
                    car.ChargerActualCurrent = 0;
                }
                if (vehicleDataResult == default)
                {
                    await errorHandlingService.HandleError(nameof(TeslaFleetApiService), nameof(RefreshCarData), $"Error while getting vehicle data for car {car.Vin}",
                        $"Could not deserialize vehicle data: {JsonConvert.SerializeObject(vehicleData)}", issueKeys.GetVehicleData, car.Vin, null).ConfigureAwait(false);
                    logger.LogError("Could not deserialize vehicle data for car {carId}: {@vehicleData}", carId, vehicleData);
                    continue;
                }
                await errorHandlingService.HandleErrorResolved(issueKeys.GetVehicleData, car.Vin);
                if (configurationWrapper.GetVehicleDataFromTesla())
                {
                    var timeStamp = dateTimeProvider.UtcNow();
                    car.Name = vehicleDataResult.VehicleState.VehicleName;
                    car.SoC = vehicleDataResult.ChargeState.BatteryLevel;
                    teslaSolarChargerContext.CarValueLogs.Add(new()
                    {
                        CarId = car.Id,
                        Timestamp = timeStamp,
                        Type = CarValueType.StateOfCharge,
                        Source = CarValueSource.FleetApi,
                        IntValue = vehicleDataResult.ChargeState.BatteryLevel,
                    });
                    car.SocLimit = vehicleDataResult.ChargeState.ChargeLimitSoc;
                    teslaSolarChargerContext.CarValueLogs.Add(new()
                    {
                        CarId = car.Id,
                        Timestamp = timeStamp,
                        Type = CarValueType.StateOfChargeLimit,
                        Source = CarValueSource.FleetApi,
                        IntValue = vehicleDataResult.ChargeState.ChargeLimitSoc,
                    });
                    var minimumSettableSocLimit = vehicleDataResult.ChargeState.ChargeLimitSocMin;
                    if (car.MinimumSoC > car.SocLimit && car.SocLimit > minimumSettableSocLimit)
                    {
                        logger.LogWarning("Reduce Minimum SoC {minimumSoC} as charge limit {chargeLimit} is lower.", car.MinimumSoC, car.SocLimit);
                        car.MinimumSoC = (int)car.SocLimit;
                        logger.LogError("Can not handle lower Soc than minimumSoc");
                    }
                    car.ChargerPhases = vehicleDataResult.ChargeState.ChargerPhases;
                    teslaSolarChargerContext.CarValueLogs.Add(new()
                    {
                        CarId = car.Id,
                        Timestamp = timeStamp,
                        Type = CarValueType.ChargerPhases,
                        Source = CarValueSource.FleetApi,
                        IntValue = vehicleDataResult.ChargeState.ChargerPhases is null or > 1 ? 3 : 1,
                    });
                    car.ChargerVoltage = vehicleDataResult.ChargeState.ChargerVoltage;
                    teslaSolarChargerContext.CarValueLogs.Add(new()
                    {
                        CarId = car.Id,
                        Timestamp = timeStamp,
                        Type = CarValueType.ChargerVoltage,
                        Source = CarValueSource.FleetApi,
                        IntValue = vehicleDataResult.ChargeState.ChargerVoltage,
                    });
                    car.ChargerActualCurrent = vehicleDataResult.ChargeState.ChargerActualCurrent;
                    teslaSolarChargerContext.CarValueLogs.Add(new()
                    {
                        CarId = car.Id,
                        Timestamp = timeStamp,
                        Type = CarValueType.ChargeAmps,
                        Source = CarValueSource.FleetApi,
                        IntValue = vehicleDataResult.ChargeState.ChargerActualCurrent,
                    });
                    car.PluggedIn = vehicleDataResult.ChargeState.ChargingState != "Disconnected";
                    teslaSolarChargerContext.CarValueLogs.Add(new()
                    {
                        CarId = car.Id,
                        Timestamp = timeStamp,
                        Type = CarValueType.IsPluggedIn,
                        Source = CarValueSource.FleetApi,
                        BooleanValue = vehicleDataResult.ChargeState.ChargingState != "Disconnected",
                    });
                    car.ClimateOn = vehicleDataResult.ClimateState.IsClimateOn;
                    car.TimeUntilFullCharge = TimeSpan.FromHours(vehicleDataResult.ChargeState.TimeToFullCharge);
                    var teslaCarStateString = vehicleDataResult.State;
                    var teslaCarShiftState = vehicleDataResult.DriveState.ShiftState;
                    var teslaCarSoftwareUpdateState = vehicleDataResult.VehicleState.SoftwareUpdate.Status;
                    var chargingState = vehicleDataResult.ChargeState.ChargingState;
                    car.State = DetermineCarState(teslaCarStateString, teslaCarShiftState, teslaCarSoftwareUpdateState, chargingState);
                    teslaSolarChargerContext.CarValueLogs.Add(new()
                    {
                        CarId = car.Id,
                        Timestamp = timeStamp,
                        Type = CarValueType.IsCharging,
                        Source = CarValueSource.FleetApi,
                        BooleanValue = vehicleDataResult.ChargeState.ChargingState != "Charging",
                    });
                    if (car.State == CarStateEnum.Unknown)
                    {
                        await errorHandlingService.HandleError(nameof(TeslaFleetApiService), nameof(RefreshCarData), $"Error determining car state for car {car.Vin}",
                            $"Could not determine car state. TeslaCarStateString: {teslaCarStateString}, TeslaCarShiftState: {teslaCarShiftState}, TeslaCarSoftwareUpdateState: {teslaCarSoftwareUpdateState}, ChargingState: {chargingState}", issueKeys.CarStateUnknown, car.Vin, null).ConfigureAwait(false);
                    }
                    else
                    {
                        await errorHandlingService.HandleErrorResolved(issueKeys.CarStateUnknown, car.Vin);
                    }
                    car.Healthy = true;
                    car.ChargerRequestedCurrent = vehicleDataResult.ChargeState.ChargeCurrentRequest;
                    teslaSolarChargerContext.CarValueLogs.Add(new()
                    {
                        CarId = car.Id,
                        Timestamp = timeStamp,
                        Type = CarValueType.ChargeCurrentRequest,
                        Source = CarValueSource.FleetApi,
                        IntValue = vehicleDataResult.ChargeState.ChargeCurrentRequest,
                    });
                    car.ChargerPilotCurrent = vehicleDataResult.ChargeState.ChargerPilotCurrent;
                    teslaSolarChargerContext.CarValueLogs.Add(new()
                    {
                        CarId = car.Id,
                        Timestamp = timeStamp,
                        Type = CarValueType.ChargerPilotCurrent,
                        Source = CarValueSource.FleetApi,
                        IntValue = vehicleDataResult.ChargeState.ChargerPilotCurrent,
                    });
                    car.ScheduledChargingStartTime = vehicleDataResult.ChargeState.ScheduledChargingStartTime == null ? (DateTimeOffset?)null : DateTimeOffset.FromUnixTimeSeconds(vehicleDataResult.ChargeState.ScheduledChargingStartTime.Value);
                    car.Longitude = vehicleDataResult.DriveState.Longitude;
                    teslaSolarChargerContext.CarValueLogs.Add(new()
                    {
                        CarId = car.Id,
                        Timestamp = timeStamp,
                        Type = CarValueType.Longitude,
                        Source = CarValueSource.FleetApi,
                        DoubleValue = vehicleDataResult.DriveState.Longitude,
                    });
                    car.Latitude = vehicleDataResult.DriveState.Latitude;
                    teslaSolarChargerContext.CarValueLogs.Add(new()
                    {
                        CarId = car.Id,
                        Timestamp = timeStamp,
                        Type = CarValueType.Latitude,
                        Source = CarValueSource.FleetApi,
                        DoubleValue = vehicleDataResult.DriveState.Latitude,
                    });
                    await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not get vehicle data for car {carId}", carId);
                await errorHandlingService.HandleError(nameof(TeslaFleetApiService), nameof(RefreshCarData), $"Error while refreshing car data for car {car.Vin}",
                    $"Error getting vehicle data: {ex.Message} {ex.StackTrace}", issueKeys.GetVehicleData, car.Vin, ex.StackTrace).ConfigureAwait(false);
            }
        }
    }

    private async Task<bool> IsCarDataRefreshNeeded(DtoCar car)
    {
        logger.LogTrace("{method}({vin})", nameof(IsCarDataRefreshNeeded), car.Vin);
        var latestRefresh = car.VehicleDataCalls.OrderByDescending(c => c).FirstOrDefault();
        if (latestRefresh == default)
        {
            latestRefresh = dateTimeProvider.UtcNow().AddDays(-1);
        }
        logger.LogDebug("Latest car refresh: {latestRefresh}", latestRefresh);
        var currentUtcDate = dateTimeProvider.UtcNow();
        var latestChangeToDetect = currentUtcDate.AddSeconds(-configurationWrapper.CarRefreshAfterCommandSeconds());
        if (fleetTelemetryWebSocketService.IsClientConnected(car.Vin))
        {
            var latestDetectedChange = latestRefresh.AddSeconds(-configurationWrapper.CarRefreshAfterCommandSeconds());
            logger.LogTrace("Fleet Telemetry Client connected, check fleet telemetry changes and do not request Fleet API after commands.");
            if (latestRefresh > latestChangeToDetect)
            {
                logger.LogDebug("Do not refresh data for car {vin} as latest refresh is {latestRefresh} and earliest change to detect is {earliestChangeToDetect}", car.Vin, latestRefresh, latestChangeToDetect);
            }
            else
            {
                if (await FleetTelemetryValueChanged(car.Id, CarValueType.IsCharging, latestDetectedChange, latestChangeToDetect).ConfigureAwait(false))
                {
                    logger.LogDebug("Send a request as Fleet Telemetry detected a change in is charging state.");
                    return true;
                }

                if (await FleetTelemetryValueChanged(car.Id, CarValueType.IsPluggedIn, latestDetectedChange, latestChangeToDetect).ConfigureAwait(false))
                {
                    logger.LogDebug("Send a request as Fleet Telemetry detected a change in plugged in state.");
                    return true;
                }

                var latestValueBeforeLatestRefresh = await GetLatestValueBeforeTimeStamp(car.Id, CarValueType.ChargeAmps, latestDetectedChange).ConfigureAwait(false);
                var latestValue = await GetLatestValueBeforeTimeStamp(car.Id, CarValueType.ChargeAmps, latestChangeToDetect).ConfigureAwait(false);
                if (latestValue != default && latestValueBeforeLatestRefresh == default)
                {
                    logger.LogDebug("Send a request as the first charging amps value was detected.");
                    return true;
                }

                if (latestValue != default && latestValueBeforeLatestRefresh != default)
                {
                    List<CarValueLogTimeStampAndValues> values =
                    [
                        latestValue,
                        latestValueBeforeLatestRefresh,
                    ];
                    if (AnyValueChanged(values) && values.Any(v => v.DoubleValue == 0))
                    {
                        logger.LogDebug("Send a request as Fleet Telemetry detected at least one 0 value in charging amps.");
                        return true;
                    }
                }
                
            }
            
        }
        else
        {
            logger.LogTrace("Fleet Telemetry Client not connected, request Fleet API after commands.");
            var homeGeofenceDistance = car.DistanceToHomeGeofence;
            var earliestHomeArrival =
                // ReSharper disable once PossibleLossOfFraction
                latestRefresh.AddSeconds((homeGeofenceDistance ?? 0) / configurationWrapper.MaxTravelSpeedMetersPerSecond());
            logger.LogDebug("Earliest Home arrival: {earliestHomeArrival}", earliestHomeArrival);
            car.EarliestHomeArrival = earliestHomeArrival;
            if (earliestHomeArrival > currentUtcDate)
            {
                logger.LogDebug("Do not refresh data for car {vin} as ealiest calculated home arrival is {ealiestHomeArrival}", car.Vin, earliestHomeArrival);
                return false;
            }

            var latestCommandTimeStamp = car.WakeUpCalls
                .Concat(car.ChargeStartCalls)
                .Concat(car.ChargeStopCalls)
                .Concat(car.SetChargingAmpsCall)
                .Concat(car.OtherCommandCalls)
                .OrderByDescending(c => c)
                .FirstOrDefault();
            if (latestCommandTimeStamp == default)
            {
                latestCommandTimeStamp = dateTimeProvider.UtcNow().AddDays(-1);
            }

            logger.LogDebug("Latest command Timestamp: {latestCommandTimeStamp}", latestCommandTimeStamp);

            //Do not waste a request if the latest command was in the last few seconds. Request the next time instead
            if (latestCommandTimeStamp > latestChangeToDetect)
            {
                logger.LogDebug("Do not refresh data as on {latestCommandTimeStamp} there was a command sent to the car.", latestCommandTimeStamp);
                return false;
            }

            //Note: This needs to be after request waste check
            if (latestCommandTimeStamp > latestRefresh)
            {
                logger.LogDebug("Send a request now as more than {carResfreshAfterCommand} s ago there was a command request", configurationWrapper.CarRefreshAfterCommandSeconds());
                return true;
            }

            var latestChargeStartOrWakeUp = car.WakeUpCalls.Concat(car.ChargeStartCalls).OrderByDescending(c => c).FirstOrDefault();
            if (latestChargeStartOrWakeUp == default)
            {
                latestChargeStartOrWakeUp = dateTimeProvider.UtcNow().AddDays(-1);
            }
            logger.LogDebug("Latest wake or charge start Timestamp: {latestChargeStartOrWakeUp}", latestChargeStartOrWakeUp);
            //force request after 55 seconds after start or wakeup as car takes much time to reach full charging speed
            const int seconds = 55;
            var forcedRequestTimeAfterStartOrWakeUp = latestChargeStartOrWakeUp + TimeSpan.FromSeconds(seconds);
            if (currentUtcDate > forcedRequestTimeAfterStartOrWakeUp
                && latestRefresh < forcedRequestTimeAfterStartOrWakeUp)
            {
                logger.LogDebug("Within the last {seconds} seconds a charge start or wake call was sent to the car. Force vehicle data call now", seconds);
                return true;
            }
        }
        

        if ((latestRefresh + configurationWrapper.FleetApiRefreshInterval()) < currentUtcDate)
        {
            logger.LogDebug("Refresh car data as time interval of {interval} s is over", configurationWrapper.FleetApiRefreshInterval());
            return true;
        }
        logger.LogDebug("Refresh of vehicle Data is not needed.");
        return false;
    }

    private async Task<bool> FleetTelemetryValueChanged(int carId, CarValueType carValueType, DateTime latestRefresh, DateTime latestChangeToDetect)
    {
        logger.LogTrace("{method}({carId}, {carValueType}, {latestRefresh}, {latestChangeToDetect})", nameof(FleetTelemetryValueChanged), carId, carValueType, latestRefresh, latestChangeToDetect);
        var values = new List<CarValueLogTimeStampAndValues>();
        var latestValueBeforeLatestRefresh = await GetLatestValueBeforeTimeStamp(carId, carValueType, latestRefresh).ConfigureAwait(false);
        var latestValue = await GetLatestValueBeforeTimeStamp(carId, carValueType, latestChangeToDetect).ConfigureAwait(false);
        if (latestValue != default && latestValueBeforeLatestRefresh == default)
        {
            //Return true if before the latest refresh there was no value, this is only relevant on new TSC installations
            return true;
        }
        if (latestValueBeforeLatestRefresh != default)
        {
            values.Add(latestValueBeforeLatestRefresh);
        }
        if (latestValue != default)
        {
            values.Add(latestValue);
        }
        return AnyValueChanged(values);
    }

    private bool AnyValueChanged(List<CarValueLogTimeStampAndValues> values)
    {
        logger.LogTrace("{method}({@values})", nameof(AnyValueChanged), values);
        // Check if any of the properties have changed among all values
        var doubleValuesChanged = values.Select(v => v.DoubleValue).Distinct().Count() > 1;
        var intValuesChanged = values.Select(v => v.IntValue).Distinct().Count() > 1;
        var stringValuesChanged = values.Select(v => v.StringValue).Distinct().Count() > 1;
        var unknownValuesChanged = values.Select(v => v.UnknownValue).Distinct().Count() > 1;
        var booleanValuesChanged = values.Select(v => v.BooleanValue).Distinct().Count() > 1;
        var invalidValuesChanged = values.Select(v => v.InvalidValue).Distinct().Count() > 1;

        // Return true if any property has changed
        return doubleValuesChanged || intValuesChanged || stringValuesChanged || unknownValuesChanged || booleanValuesChanged || invalidValuesChanged;
    }

    private async Task<CarValueLogTimeStampAndValues?> GetLatestValueBeforeTimeStamp(int carId, CarValueType carValueType, DateTime timestamp)
    {
        logger.LogTrace("{method}({carId}, {carValueType}, {timestamp})", nameof(GetLatestValueBeforeTimeStamp), carId, carValueType, timestamp);
        var lastBeforeStartTimeValue = await teslaSolarChargerContext.CarValueLogs
            .Where(c => c.Type == carValueType
                        && c.Source == CarValueSource.FleetTelemetry
                        && c.CarId == carId
                        && c.Timestamp < timestamp)
            .OrderByDescending(c => c.Timestamp)
            .Select(c => new CarValueLogTimeStampAndValues
            {
                Timestamp = c.Timestamp,
                DoubleValue = c.DoubleValue,
                IntValue = c.IntValue,
                StringValue = c.StringValue,
                UnknownValue = c.UnknownValue,
                BooleanValue = c.BooleanValue,
                InvalidValue = c.InvalidValue,
            })
            .FirstOrDefaultAsync();
        return lastBeforeStartTimeValue;
    }


    private CarStateEnum? DetermineCarState(string teslaCarStateString, string? teslaCarShiftState, string teslaCarSoftwareUpdateState, string chargingState)
    {
        logger.LogTrace("{method}({teslaCarStateString}, {teslaCarShiftState}, {teslaCarSoftwareUpdateState}, {chargingState})", nameof(DetermineCarState), teslaCarStateString, teslaCarShiftState, teslaCarSoftwareUpdateState, chargingState);
        if (teslaCarStateString == "asleep")
        {
            return CarStateEnum.Asleep;
        }

        if (teslaCarStateString == "offline")
        {
            return CarStateEnum.Offline;
        }
        if (teslaCarShiftState is "R" or "D")
        {
            return CarStateEnum.Driving;
        }
        if (chargingState == "Charging")
        {
            return CarStateEnum.Charging;
        }
        if (teslaCarSoftwareUpdateState == "installing")
        {
            return CarStateEnum.Updating;
        }
        if (teslaCarStateString == "online")
        {
            return CarStateEnum.Online;
        }
        logger.LogWarning("Could not determine car state. TeslaCarStateString: {teslaCarStateString}, TeslaCarShiftState: {teslaCarShiftState}, TeslaCarSoftwareUpdateState: {teslaCarSoftwareUpdateState}, ChargingState: {chargingState}", teslaCarStateString, teslaCarShiftState, teslaCarSoftwareUpdateState, chargingState);
        return CarStateEnum.Unknown;
    }

    private string GetVinByCarId(int carId)
    {
        var vin = settings.Cars.First(c => c.Id == carId).Vin;
        return vin;
    }

    internal bool IsChargingScheduleChangeNeeded(DateTimeOffset? chargingStartTime, DateTimeOffset currentDate, DtoCar dtoCar, out Dictionary<string, string> parameters)
    {
        logger.LogTrace("{method}({startTime}, {currentDate}, {carId}, {parameters})", nameof(IsChargingScheduleChangeNeeded), chargingStartTime, currentDate, dtoCar.Id, nameof(parameters));
        parameters = new Dictionary<string, string>();
        if (chargingStartTime != null)
        {
            logger.LogTrace("{chargingStartTime} is not null", nameof(chargingStartTime));
            chargingStartTime = RoundToNextQuarterHour(chargingStartTime.Value);
        }
        if (dtoCar.ScheduledChargingStartTime == chargingStartTime)
        {
            logger.LogDebug("Correct charging start time already set.");
            return false;
        }

        if (chargingStartTime == null)
        {
            logger.LogDebug("Set chargingStartTime to null.");
            parameters = new Dictionary<string, string>()
            {
                { "enable", "false" },
                { "time", 0.ToString() },
            };
            return true;
        }

        var localStartTime = chargingStartTime.Value.ToLocalTime().TimeOfDay;
        var minutesFromMidNight = (int)localStartTime.TotalMinutes;
        var timeUntilChargeStart = chargingStartTime.Value - currentDate;
        var scheduledChargeShouldBeSet = true;

        if (dtoCar.ScheduledChargingStartTime == chargingStartTime)
        {
            logger.LogDebug("Correct charging start time already set.");
            return true;
        }

        //ToDo: maybe disable scheduled charge in this case.
        if (timeUntilChargeStart <= TimeSpan.Zero || timeUntilChargeStart.TotalHours > 24)
        {
            logger.LogDebug("Charge schedule should not be changed, as time until charge start is higher than 24 hours or lower than zero.");
            return false;
        }

        if (dtoCar.ScheduledChargingStartTime == null && !scheduledChargeShouldBeSet)
        {
            logger.LogDebug("No charge schedule set and no charge schedule should be set.");
            return true;
        }
        logger.LogDebug("Normal parameter set.");
        parameters = new Dictionary<string, string>()
        {
            { "enable", scheduledChargeShouldBeSet ? "true" : "false" },
            { "time", minutesFromMidNight.ToString() },
        };
        logger.LogTrace("{@parameters}", parameters);
        return true;
    }

    internal DateTimeOffset RoundToNextQuarterHour(DateTimeOffset chargingStartTime)
    {
        var maximumTeslaChargeStartAccuracyMinutes = 15;
        var minutes = chargingStartTime.Minute; // Aktuelle Minute des DateTimeOffset-Objekts

        // Runden auf die nächste viertel Stunde
        var roundedMinutes = (int)Math.Ceiling((double)minutes / maximumTeslaChargeStartAccuracyMinutes) *
                             maximumTeslaChargeStartAccuracyMinutes;
        var additionalHours = 0;
        if (roundedMinutes == 60)
        {
            roundedMinutes = 0;
            additionalHours = 1;
        }

        var newNotRoundedDateTime = chargingStartTime.AddHours(additionalHours);
        chargingStartTime = new DateTimeOffset(newNotRoundedDateTime.Year, newNotRoundedDateTime.Month,
            newNotRoundedDateTime.Day, newNotRoundedDateTime.Hour, roundedMinutes, 0, newNotRoundedDateTime.Offset);
        logger.LogDebug("Rounded charging Start time: {chargingStartTime}", chargingStartTime);
        return chargingStartTime;
    }

    private async Task WakeUpCarIfNeeded(int carId, CarStateEnum? carState)
    {
        if (carState is CarStateEnum.Asleep or CarStateEnum.Offline or CarStateEnum.Suspended)
        {
            var car = settings.Cars.First(c => c.Id == carId);
            switch (carState)
            {
                case CarStateEnum.Offline or CarStateEnum.Asleep:
                    logger.LogInformation("Wakeup car.");
                    await WakeUpCar(carId).ConfigureAwait(false);
                    break;
                case CarStateEnum.Suspended:
                    logger.LogInformation("Resume logging as is suspended");
                    if (car.TeslaMateCarId != default)
                    {
                        //ToDo: fix with https://github.com/pkuehnel/TeslaSolarCharger/issues/1511
                        //await teslamateApiService.ResumeLogging(car.TeslaMateCarId.Value).ConfigureAwait(false);
                    }
                    break;
            }
        }
        
    }

    private async Task<DtoGenericTeslaResponse<T>?> SendCommandToTeslaApi<T>(string vin, DtoFleetApiRequest fleetApiRequest, HttpMethod httpMethod, string contentData = "{}", int? amp = null) where T : class
    {
        logger.LogTrace("{method}({vin}, {@fleetApiRequest}, {contentData})", nameof(SendCommandToTeslaApi), vin, fleetApiRequest, contentData);
        var car = settings.Cars.First(c => c.Vin == vin);
        if (fleetApiRequest.BleCompatible)
        {
            
            var isCarBleEnabled = car.UseBle;
            if (isCarBleEnabled)
            {
                //When changing this condition also change it in ErrorHandlingService.DetectErrors as there the error will be set to resolved.
                if ((car.LastNonSuccessBleCall != default) && (car.LastNonSuccessBleCall.Value >
                    (dateTimeProvider.UtcNow() - configurationWrapper.BleUsageStopAfterError())))
                {
                    logger.LogWarning("BLE is not used for car {carVin} as last non success BLE call was at {lastNonSuccessBleCall}", car.Vin, car.LastNonSuccessBleCall);
                }
                else
                {
                    var result = new DtoBleCommandResult();
                    try
                    {
                        if (fleetApiRequest.RequestUrl == ChargeStartRequest.RequestUrl)
                        {
                            result = await bleService.StartCharging(vin);
                        }
                        else if (fleetApiRequest.RequestUrl == ChargeStopRequest.RequestUrl)
                        {
                            result = await bleService.StopCharging(vin);
                        }
                        else if (fleetApiRequest.RequestUrl == SetChargingAmpsRequest.RequestUrl)
                        {
                            result = await bleService.SetAmp(vin, amp!.Value);
                        }
                        else if (fleetApiRequest.RequestUrl == WakeUpRequest.RequestUrl)
                        {
                            result = await bleService.WakeUpCar(vin);
                        }

                        if (result.Success
                            || (result.ErrorType == ErrorType.CarExecution
                                && (fleetApiRequest.RequestUrl == ChargeStartRequest.RequestUrl)
                                && (result.CarErrorMessage?.Contains(IsChargingErrorMessage) == true))
                            || (result.ErrorType == ErrorType.CarExecution
                                && (fleetApiRequest.RequestUrl == ChargeStopRequest.RequestUrl)
                                && (result.CarErrorMessage?.Contains(IsNotChargingErrorMessage) == true)))
                        {
                            AddRequestToCar(vin, fleetApiRequest);
                            await errorHandlingService.HandleErrorResolved(issueKeys.BleCommandNoSuccess + fleetApiRequest.RequestUrl, car.Vin);
                            await errorHandlingService.HandleErrorResolved(issueKeys.UsingFleetApiAsBleFallback, car.Vin);
                            if (typeof(T) == typeof(DtoVehicleCommandResult))
                            {
                                var comamndResult = new DtoGenericTeslaResponse<T> { Response = (T)(object)new DtoVehicleCommandResult()
                                    {
                                        //Do not use result.Success as on is_charging and not_charging errors this would be false but should be true
                                        Result = true,
                                        Reason = result.ResultMessage ?? string.Empty,
                                    },
                                };
                                return comamndResult;
                            }

                            return new();
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error while calling BLE API");
                    }


                    await errorHandlingService.HandleError(nameof(TeslaFleetApiService), nameof(SendCommandToTeslaApi), $"Error sending BLE command for car {car.Vin}",
                        $"Sending command to tesla via BLE did not succeed. Fleet API URL would be: {fleetApiRequest.RequestUrl}. BLE Response: {result.ResultMessage}",
                        issueKeys.BleCommandNoSuccess + fleetApiRequest.RequestUrl, car.Vin, null).ConfigureAwait(false);
                    car.LastNonSuccessBleCall = dateTimeProvider.UtcNow();
                    var fallbackUntilLocalTimeString =
                        (car.LastNonSuccessBleCall + configurationWrapper.BleUsageStopAfterError()).Value.ToLocalTime();
                    logger.LogWarning("Command BLE enabled but command did not succeed, using Fleet API as fallback until {fallbackUntil}.", fallbackUntilLocalTimeString);
                    await errorHandlingService.HandleError(nameof(TeslaFleetApiService), nameof(SendCommandToTeslaApi),
                        $"Using Fleet API as BLE fallback for car {car.Vin}",
                        $"As the BLE command did not succeed, Fleet API is used as fallback until {fallbackUntilLocalTimeString}. Note: During this time it is not possible to retry BLE automatically you need to go to the car settings page and test BLE access manually.",
                        issueKeys.UsingFleetApiAsBleFallback, car.Vin, null).ConfigureAwait(false);
                }
                
            }
        }
        var accessToken = await GetAccessToken().ConfigureAwait(false);
        if (accessToken == default)
        {
            logger.LogError("Access token not found do not send command");
            return null;
        }
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.AccessToken);
        var content = new StringContent(contentData, System.Text.Encoding.UTF8, "application/json");
        var rateLimitedUntil = await RateLimitedUntil(vin, fleetApiRequest.TeslaApiRequestType).ConfigureAwait(false);
        var currentDate = dateTimeProvider.UtcNow();
        if (currentDate < rateLimitedUntil)
        {
            logger.LogError("Car with VIN {vin} rate limited until {rateLimitedUntil}. Skipping command.", vin, rateLimitedUntil);
            await errorHandlingService.HandleError(nameof(TeslaFleetApiService), nameof(SendCommandToTeslaApi), $"Car {car.Vin} is rate limited",
                $"Car is rate limited until {rateLimitedUntil}", issueKeys.CarRateLimited, car.Vin, null);
            return null;
        }
        await errorHandlingService.HandleErrorResolved(issueKeys.CarRateLimited, car.Vin);
        var fleetApiProxyRequired = await IsFleetApiProxyEnabled(vin).ConfigureAwait(false);
        var baseUrl = GetFleetApiBaseUrl(accessToken.Region, fleetApiRequest.NeedsProxy, fleetApiProxyRequired.Value);
        var requestUri = $"{baseUrl}api/1/vehicles/{vin}/{fleetApiRequest.RequestUrl}";
        var request = new HttpRequestMessage()
        {
            Content = content,
            RequestUri = new Uri(requestUri),
            Method = httpMethod,
        };
        var response = await httpClient.SendAsync(request).ConfigureAwait(false);
        var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        logger.LogTrace("Response status code: {statusCode}", response.StatusCode);
        logger.LogTrace("Response string: {responseString}", responseString);
        logger.LogTrace("Response headers: {@headers}", response.Headers);
        if (response.IsSuccessStatusCode)
        {
            AddRequestToCar(vin, fleetApiRequest);
            await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiNonSuccessStatusCode + fleetApiRequest.RequestUrl, car.Vin);
        }
        else
        {
            await errorHandlingService.HandleError(nameof(TeslaFleetApiService), nameof(SendCommandToTeslaApi), $"Error while sending command to car {car.Vin}",
                $"Sending command to Tesla API resulted in non succes status code: {response.StatusCode} : Command name:{fleetApiRequest.RequestUrl}, Content data:{contentData}. Response string: {responseString}",
                issueKeys.FleetApiNonSuccessStatusCode + fleetApiRequest.RequestUrl, car.Vin, null).ConfigureAwait(false);
            logger.LogError("Sending command to Tesla API resulted in non succes status code: {statusCode} : Command name:{commandName}, Content data:{contentData}. Response string: {responseString}", response.StatusCode, fleetApiRequest.RequestUrl, contentData, responseString);
            await HandleNonSuccessTeslaApiStatusCodes(response.StatusCode, accessToken, responseString, fleetApiRequest.TeslaApiRequestType, vin).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                return new() { Error = responseString, ErrorDescription = "ServiceUnavailable", };
            }
        }
        var teslaCommandResultResponse = JsonConvert.DeserializeObject<DtoGenericTeslaResponse<T>>(responseString);
        if (response.IsSuccessStatusCode && (teslaCommandResultResponse?.Response is DtoVehicleCommandResult vehicleCommandResult))
        {
            if (vehicleCommandResult.Result != true
                && !((fleetApiRequest.RequestUrl == ChargeStartRequest.RequestUrl) && responseString.Contains(IsChargingErrorMessage))
                && !((fleetApiRequest.RequestUrl == ChargeStopRequest.RequestUrl) && responseString.Contains(IsNotChargingErrorMessage)))
            {
                await errorHandlingService.HandleError(nameof(TeslaFleetApiService), nameof(SendCommandToTeslaApi), $"Car {car.Vin} could not handle command",
                        $"Result of command request is false {fleetApiRequest.RequestUrl}, {contentData}. Response string: {responseString}",
                        issueKeys.FleetApiNonSuccessResult + fleetApiRequest.RequestUrl, car.Vin, null)
                    .ConfigureAwait(false);
                logger.LogError("Result of command request is false {fleetApiRequest.RequestUrl}, {contentData}. Response string: {responseString}", fleetApiRequest.RequestUrl, contentData, responseString);
                await HandleUnsignedCommands(vehicleCommandResult, vin).ConfigureAwait(false);
            }
            else
            {
                await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiNonSuccessResult + fleetApiRequest.RequestUrl, car.Vin);
            }
        }
        logger.LogDebug("Response: {responseString}", responseString);
        return teslaCommandResultResponse;
    }

    public void ResetApiRequestCounters()
    {
        logger.LogTrace("{method}()", nameof(ResetApiRequestCounters));
        var currentUtcDate = dateTimeProvider.UtcNow().Date;
        foreach (var car in settings.Cars)
        {
            car.WakeUpCalls.RemoveAll(d => d < currentUtcDate);
            car.VehicleDataCalls.RemoveAll(d => d < currentUtcDate);
            car.VehicleCalls.RemoveAll(d => d < currentUtcDate);
            car.ChargeStartCalls.RemoveAll(d => d < currentUtcDate);
            car.ChargeStopCalls.RemoveAll(d => d < currentUtcDate);
            car.SetChargingAmpsCall.RemoveAll(d => d < currentUtcDate);
            car.OtherCommandCalls.RemoveAll(d => d < currentUtcDate);
        }
    }

    private void AddRequestToCar(string vin, DtoFleetApiRequest fleetApiRequest)
    {
        logger.LogTrace("{method}({@fleetApiRequest})", nameof(AddRequestToCar), fleetApiRequest);
        var car = settings.Cars.FirstOrDefault(c => c.Vin == vin);
        if (car == default)
        {
            logger.LogError("Could find car for request logging");
            return;
        }
        var currentDate = dateTimeProvider.UtcNow();
        if (fleetApiRequest.RequestUrl == ChargeStartRequest.RequestUrl)
        {
            car.ChargeStartCalls.Add(currentDate);
        }
        else if (fleetApiRequest.RequestUrl == ChargeStopRequest.RequestUrl)
        {
            car.ChargeStopCalls.Add(currentDate);
        }
        else if (fleetApiRequest.RequestUrl == SetChargingAmpsRequest.RequestUrl)
        {
            car.SetChargingAmpsCall.Add(currentDate);
        }
        else if (fleetApiRequest.RequestUrl == SetScheduledChargingRequest.RequestUrl)
        {
            car.OtherCommandCalls.Add(currentDate);
        }
        else if (fleetApiRequest.RequestUrl == SetChargeLimitRequest.RequestUrl)
        {
            car.OtherCommandCalls.Add(currentDate);
        }
        else if (fleetApiRequest.RequestUrl == FlashHeadlightsRequest.RequestUrl)
        {
            car.OtherCommandCalls.Add(currentDate);
        }
        else if (fleetApiRequest.RequestUrl == WakeUpRequest.RequestUrl)
        {
            car.WakeUpCalls.Add(currentDate);
        }
        else if (fleetApiRequest.RequestUrl == VehicleRequest.RequestUrl)
        {
            car.VehicleCalls.Add(currentDate);
        }
        else if (fleetApiRequest.RequestUrl == VehicleDataRequest.RequestUrl)
        {
            car.VehicleDataCalls.Add(currentDate);
        }
    }

    private async Task<DateTime?> RateLimitedUntil(string vin, TeslaApiRequestType teslaApiRequestType)
    {
        logger.LogTrace("{method}({vin}, {teslaApiRequestType})", nameof(RateLimitedUntil), vin, teslaApiRequestType);
        var rateLimitedUntil = await GetRateLimitInfos(vin);

        return teslaApiRequestType switch
        {
            TeslaApiRequestType.Vehicle => rateLimitedUntil.VehicleRateLimitedUntil,
            TeslaApiRequestType.VehicleData => rateLimitedUntil.VehicleDataRateLimitedUntil,
            TeslaApiRequestType.Command => rateLimitedUntil.CommandsRateLimitedUntil,
            TeslaApiRequestType.WakeUp => rateLimitedUntil.WakeUpRateLimitedUntil,
            TeslaApiRequestType.Charging => rateLimitedUntil.ChargingCommandsRateLimitedUntil,
            TeslaApiRequestType.Other => null,
            _ => throw new ArgumentOutOfRangeException(nameof(teslaApiRequestType), teslaApiRequestType, null)
        };
    }

    private async Task<DtoRateLimitInfos> GetRateLimitInfos(string vin)
    {
        var rateLimitedUntil = await teslaSolarChargerContext.Cars
            .Where(c => c.Vin == vin)
            .Select(c => new DtoRateLimitInfos()
            {
                VehicleRateLimitedUntil = c.VehicleRateLimitedUntil,
                VehicleDataRateLimitedUntil = c.VehicleDataRateLimitedUntil,
                CommandsRateLimitedUntil = c.CommandsRateLimitedUntil,
                ChargingCommandsRateLimitedUntil = c.ChargingCommandsRateLimitedUntil,
                WakeUpRateLimitedUntil = c.WakeUpRateLimitedUntil,
            })
            .FirstAsync();
        return rateLimitedUntil;
    }

    private async Task HandleUnsignedCommands(DtoVehicleCommandResult vehicleCommandResult, string vin)
    {
        if (string.Equals(vehicleCommandResult.Reason, "unsigned_cmds_hardlocked"))
        {
            await errorHandlingService.HandleError(nameof(TeslaFleetApiService), nameof(SendCommandToTeslaApi), $"Car {vin} needs a fleet key",
                    "FleetAPI proxy needed set to true", issueKeys.UnsignedCommand, vin, null)
                .ConfigureAwait(false);
            if (!(await IsFleetApiProxyEnabled(vin).ConfigureAwait(false)).Value)
            {
                var car = teslaSolarChargerContext.Cars.First(c => c.Vin == vin);
                car.VehicleCommandProtocolRequired = true;
                await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
            }
        }
        else
        {
            await errorHandlingService.HandleErrorResolved(issueKeys.UnsignedCommand, vin);
        }
    }

    private string GetFleetApiBaseUrl(TeslaFleetApiRegion region, bool useProxyBaseUrl, bool fleetApiProxyRequired)
    {
        if (useProxyBaseUrl && fleetApiProxyRequired)
        {
            var configUrl = configurationWrapper.GetFleetApiBaseUrl();
            return configUrl ?? throw new KeyNotFoundException("Could not get Tesla HTTP proxy address");
        }

        if (region == TeslaFleetApiRegion.China)
        {
            return "https://fleet-api.prd.cn.vn.cloud.tesla.cn";
        }
        var regionCode = region switch
        {
            TeslaFleetApiRegion.Emea => "eu",
            TeslaFleetApiRegion.NorthAmerica => "na",
            _ => throw new NotImplementedException($"Region {region} is not implemented."),
        };
        return $"https://fleet-api.prd.{regionCode}.vn.cloud.tesla.com/";
    }

    /// <summary>
    /// Get a new Token from TSC Backend
    /// </summary>
    /// <returns>True if a new Token was received</returns>
    /// <exception cref="InvalidDataException">Token could not be extracted from result string</exception>
    public async Task<bool> GetNewTokenFromBackend()
    {
        logger.LogTrace("{method}()", nameof(GetNewTokenFromBackend));
        //As all tokens get deleted when requesting a new one, we can assume that there is no token in the database.
        var token = await teslaSolarChargerContext.TeslaTokens.FirstOrDefaultAsync().ConfigureAwait(false);
        if (token != null)
        {
            return false;
        }

        var tokenRequestedDate = await teslaFleetApiTokenHelper.GetTokenRequestedDate().ConfigureAwait(false);
        if (tokenRequestedDate == null)
        {
            logger.LogError("Token has not been requested. Fleet API currently not working");
            return false;
        }
        if (tokenRequestedDate < dateTimeProvider.UtcNow().Subtract(constants.MaxTokenRequestWaitTime))
        {
            logger.LogError("Last token request is too old. Request a new token.");
            await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiTokenNotReceived, null).ConfigureAwait(false);
            return false;
        }
        using var httpClient = new HttpClient();
        var installationId = await tscConfigurationService.GetInstallationId().ConfigureAwait(false);
        var url = configurationWrapper.BackendApiBaseUrl() + $"Tsc/DeliverAuthToken?installationId={installationId}";
        var response = await httpClient.GetAsync(url).ConfigureAwait(false);
        var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Error getting token from TSC Backend. Response status code: {statusCode}, Response string: {responseString}",
                response.StatusCode, responseString);
            return false;

        }

        var newToken = JsonConvert.DeserializeObject<DtoTeslaTscDeliveryToken>(responseString) ?? throw new InvalidDataException("Could not get token from string.");
        await AddNewTokenAsync(newToken).ConfigureAwait(false);
        await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiTokenNotReceived, null).ConfigureAwait(false);
        return true;
    }

    public async Task RefreshTokensIfAllowedAndNeeded()
    {
        logger.LogTrace("{method}()", nameof(RefreshTokensIfAllowedAndNeeded));
        var tokens = await teslaSolarChargerContext.TeslaTokens.ToListAsync().ConfigureAwait(false);
        if (tokens.Count < 1)
        {
            logger.LogError("No token found. Cannot refresh token.");
            return;
        }

        var tokensToRefresh = tokens.Where(t => t.ExpiresAtUtc < (dateTimeProvider.UtcNow() + TimeSpan.FromMinutes(2))).ToList();
        if (tokensToRefresh.Count < 1)
        {
            logger.LogTrace("No token needs to be refreshed.");
            return;
        }

        foreach (var tokenToRefresh in tokensToRefresh)
        {
            logger.LogWarning("Token {tokenId} needs to be refreshed as it expires on {expirationDateTime}", tokenToRefresh.Id, tokenToRefresh.ExpiresAtUtc);

            //DO NOTE REMOVE *2: As normal requests could result in reaching max unauthorized count, the max value is higher here, so even if token is unauthorized, refreshing it is still tried a couple of times.
            if (tokenToRefresh.UnauthorizedCounter > (constants.MaxTokenUnauthorizedCount * 2))
            {
                logger.LogError("Token {tokenId} has been unauthorized too often. Do not refresh token.", tokenToRefresh.Id);
                continue;
            }
            using var httpClient = new HttpClient();
            var tokenUrl = "https://auth.tesla.com/oauth2/v3/token";
            var requestData = new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "client_id", configurationWrapper.FleetApiClientId() },
                { "refresh_token", tokenToRefresh.RefreshToken },
            };
            var encodedContent = new FormUrlEncodedContent(requestData);
            encodedContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
            var response = await httpClient.PostAsync(tokenUrl, encodedContent).ConfigureAwait(false);
            var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiTokenRefreshNonSuccessStatusCode, null);
            }
            else
            {
                await errorHandlingService.HandleError(nameof(TeslaFleetApiService), nameof(SendCommandToTeslaApi), "Tesla Fleet API token could not be refreshed",
                    $"Refreshing token did result in non success status code. Response status code: {response.StatusCode} Response string: {responseString}",
                    issueKeys.FleetApiTokenRefreshNonSuccessStatusCode, null, null).ConfigureAwait(false);
                logger.LogError("Refreshing token did result in non success status code. Response status code: {statusCode} Response string: {responseString}", response.StatusCode, responseString);
                await HandleNonSuccessTeslaApiStatusCodes(response.StatusCode, tokenToRefresh, responseString, TeslaApiRequestType.Other).ConfigureAwait(false);
            }
            response.EnsureSuccessStatusCode();
            if (settings.AllowUnlimitedFleetApiRequests == false)
            {
                logger.LogError("Due to rate limitations fleet api requests are not allowed. As this version can not handle rate limits try updating to the latest version.");
                teslaSolarChargerContext.TeslaTokens.Remove(tokenToRefresh);
                await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
                return;
            }
            var newToken = JsonConvert.DeserializeObject<DtoTeslaFleetApiRefreshToken>(responseString) ?? throw new InvalidDataException("Could not get token from string.");
            tokenToRefresh.AccessToken = newToken.AccessToken;
            tokenToRefresh.RefreshToken = newToken.RefreshToken;
            tokenToRefresh.IdToken = newToken.IdToken;
            tokenToRefresh.ExpiresAtUtc = dateTimeProvider.UtcNow().AddSeconds(newToken.ExpiresIn);
            tokenToRefresh.UnauthorizedCounter = 0;
            await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
            logger.LogInformation("New Token saved to database.");
        }
    }

    public async Task RefreshFleetApiRequestsAreAllowed()
    {
        logger.LogTrace("{method}()", nameof(RefreshFleetApiRequestsAreAllowed));
        if (settings.AllowUnlimitedFleetApiRequests && (settings.LastFleetApiRequestAllowedCheck > dateTimeProvider.UtcNow().AddHours(-1)))
        {
            return;
        }
        settings.LastFleetApiRequestAllowedCheck = dateTimeProvider.UtcNow();
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(2);
        var installationId = await tscConfigurationService.GetInstallationId().ConfigureAwait(false);
        var url = configurationWrapper.BackendApiBaseUrl() + $"Tsc/AllowUnlimitedFleetApiAccess?installationId={installationId}";
        try
        {
            var response = await httpClient.GetAsync(url).ConfigureAwait(false);
            var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                settings.AllowUnlimitedFleetApiRequests = true;
                return;
            }

            var responseValue = JsonConvert.DeserializeObject<DtoValue<bool>>(responseString);
            settings.AllowUnlimitedFleetApiRequests = responseValue?.Value == true;
        }
        catch (Exception)
        {
            settings.AllowUnlimitedFleetApiRequests = false;
        }
        
    }

    public async Task AddNewTokenAsync(DtoTeslaTscDeliveryToken token)
    {
        var currentTokens = await teslaSolarChargerContext.TeslaTokens.ToListAsync().ConfigureAwait(false);
        teslaSolarChargerContext.TeslaTokens.RemoveRange(currentTokens);
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        teslaSolarChargerContext.TeslaTokens.Add(new TeslaToken
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            IdToken = token.IdToken,
            ExpiresAtUtc = dateTimeProvider.UtcNow().AddSeconds(token.ExpiresIn),
            Region = token.Region,
            UnauthorizedCounter = 0,
        });
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<DtoValue<FleetApiTokenState>> GetFleetApiTokenState()
    {
        var tokenState = await teslaFleetApiTokenHelper.GetFleetApiTokenState();
        return new(tokenState);
    }

    

    private async Task<TeslaToken?> GetAccessToken()
    {
        logger.LogTrace("{method}()", nameof(GetAccessToken));
        var token = await teslaSolarChargerContext.TeslaTokens
            .OrderByDescending(t => t.ExpiresAtUtc)
            .FirstOrDefaultAsync().ConfigureAwait(false);
        if (token != default && token.UnauthorizedCounter > constants.MaxTokenUnauthorizedCount)
        {
            logger.LogError("Token unauthorized counter is too high. Request a new token.");
            throw new InvalidOperationException("Token unauthorized counter is too high. Request a new token.");
        }
        return token;
    }

    private async Task HandleNonSuccessTeslaApiStatusCodes(HttpStatusCode statusCode, TeslaToken token,
        string responseString, TeslaApiRequestType teslaApiRequestType, string? vin = null)
    {
        logger.LogTrace("{method}({statusCode}, {token}, {responseString})", nameof(HandleNonSuccessTeslaApiStatusCodes), statusCode, token, responseString);
        if (statusCode == HttpStatusCode.Unauthorized)
        {
            logger.LogError(
                "Your token or refresh token is invalid. Very likely you have changed your Tesla password. Current unauthorized counter {unauthorizedCounter}, Should have been valid until: {expiresAt}, Response: {responseString}",
                ++token.UnauthorizedCounter, token.ExpiresAtUtc, responseString);
        }
        else if (statusCode == HttpStatusCode.Forbidden)
        {
            if (responseString.Contains("Tesla Vehicle Command Protocol required"))
            {
                var car = teslaSolarChargerContext.Cars.First(c => c.Vin == vin);
                car.VehicleCommandProtocolRequired = true;
            }
            else
            {
                logger.LogError("You did not select all scopes, so TSC can't send commands to your car. Response: {responseString}", responseString);
                teslaSolarChargerContext.TscConfigurations.Add(new TscConfiguration()
                {
                    Key = constants.TokenMissingScopes,
                    Value = responseString,
                });
            }
            
        }
        else if (statusCode == HttpStatusCode.InternalServerError
                 && responseString.Contains("vehicle rejected request: your public key has not been paired with the vehicle"))
        {
            logger.LogError("Vehicle {vin} is not paired with TSC. Add The public key to the vehicle. Response: {responseString}", vin, responseString);
            var car = teslaSolarChargerContext.Cars.First(c => c.Vin == vin);
            car.TeslaFleetApiState = TeslaCarFleetApiState.NotWorking;
        }
        else if (statusCode == HttpStatusCode.TooManyRequests)
        {
            logger.LogWarning("Too many requests to Tesla API for car {vin}. {responseString}", vin, responseString);
            var car = teslaSolarChargerContext.Cars.First(c => c.Vin == vin);
            var nextAllowedUtcTime = GetUtcTimeFromRetryInSeconds(responseString);
            switch (teslaApiRequestType)
            {
                case TeslaApiRequestType.Vehicle:
                    car.VehicleRateLimitedUntil = nextAllowedUtcTime;
                    break;
                case TeslaApiRequestType.VehicleData:
                    car.VehicleDataRateLimitedUntil = nextAllowedUtcTime;
                    break;
                case TeslaApiRequestType.Command:
                    car.CommandsRateLimitedUntil = nextAllowedUtcTime;
                    break;
                case TeslaApiRequestType.WakeUp:
                    car.WakeUpRateLimitedUntil = nextAllowedUtcTime;
                    break;
                case TeslaApiRequestType.Charging:
                    car.ChargingCommandsRateLimitedUntil = nextAllowedUtcTime;
                    break;
                case TeslaApiRequestType.Other:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(teslaApiRequestType), teslaApiRequestType, null);
            }
        }
        else
        {
            logger.LogWarning(
                "Status Code {statusCode} is currently not handled, look into https://developer.tesla.com/docs/fleet-api#response-codes to check status code information. Response: {responseString}",
                statusCode, responseString);
            return;
        }

        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }

    private DateTime GetUtcTimeFromRetryInSeconds(string responseString)
    {
        logger.LogTrace("{method}({responseString})", nameof(GetUtcTimeFromRetryInSeconds), responseString);

        var retryInSeconds = RetryInSeconds(responseString);
        var nextAllowedUtcTime = dateTimeProvider.UtcNow().AddSeconds(retryInSeconds);
        return nextAllowedUtcTime;
    }

    public async Task<Fin<List<DtoTesla>>> GetNewCarsInAccount()
    {
        logger.LogTrace("{method}()", nameof(GetNewCarsInAccount));
        var result = await GetAllCarsFromAccount();
        return result.Map(carList =>
        {
            // Filter the list for new cars
            var newCars = carList
                .Where(c => settings.Cars.Any(sc => string.Equals(sc.Vin, c.Vin, StringComparison.CurrentCultureIgnoreCase)))
                .ToList();
            return newCars;
        });
    }

    public async Task<Fin<List<DtoTesla>>> GetAllCarsFromAccount()
    {
        logger.LogTrace("{method}()", nameof(GetAllCarsFromAccount));
        var accessToken = await GetAccessToken().ConfigureAwait(false);
        if (accessToken == default || accessToken.ExpiresAtUtc < dateTimeProvider.UtcNow())
        {
            logger.LogError("Can not add cars to TSC as no Tesla Token was found");
            return Fin<List<DtoTesla>>.Fail("No Tesla token found or existing token expired."); ;
        }
        var baseUrl = GetFleetApiBaseUrl(accessToken.Region, false, false);
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.AccessToken);
        var requestUri = $"{baseUrl}api/1/vehicles";
        var request = new HttpRequestMessage()
        {
            RequestUri = new Uri(requestUri),
            Method = HttpMethod.Get,
        };
        try
        {
            var response = await httpClient.SendAsync(request);
            var responseBodyString = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Error while getting all cars from account: Status code: {statusCode}; Response Body: {responseBodyString}", response.StatusCode, responseBodyString);
                var excpetion = new HttpRequestException($"Requesting {requestUri} returned following body: {responseBodyString}", null,
                    response.StatusCode);
                return Fin<List<DtoTesla>>.Fail(Error.New(excpetion));
            }

            
            var vehicles = JsonConvert.DeserializeObject<DtoGenericTeslaResponse<List<DtoVehicleResult>>>(responseBodyString);

            if (vehicles?.Response == null)
            {
                logger.LogError("Could not deserialize vehicle list response body {responseBodyString}", responseBodyString);
                return Fin<List<DtoTesla>>.Fail($"Could not deserialize response body {responseBodyString}");
            }

            // Convert TeslaVehicle to DtoTesla
            var dtos = vehicles.Response.Select(v => new DtoTesla { Name = v.DisplayName, Vin = v.Vin }).ToList();
            return Fin<List<DtoTesla>>.Succ(dtos);
        }
        catch (HttpRequestException e)
        {
            logger.LogError(e,"An HTTP request error occured");
            return Fin<List<DtoTesla>>.Fail(Error.New(e));
        }
        catch (JsonException e)
        {
            logger.LogError(e, "Failed to parse JSON response");
            return Fin<List<DtoTesla>>.Fail(Error.New(e));
        }
        catch (Exception e)
        {
            logger.LogError(e, "An unexpected error occurred");
            return Fin<List<DtoTesla>>.Fail(Error.New(e));
        }
    }

    internal int RetryInSeconds(string responseString)
    {
        logger.LogTrace("{method}({responseString})", nameof(RetryInSeconds), responseString);
        try
        {
            var retryInSeconds = int.Parse(responseString.Split("Retry in ")[1].Split(" seconds")[0]);
            return retryInSeconds;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not parse retry in seconds from response string: {responseString}", responseString);
            return 0;
        }
    }
}
