using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System.Net;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Dtos.Solar4CarBackend;
using TeslaSolarCharger.Server.Dtos.TeslaFleetApi;
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
    ITokenHelper tokenHelper,
    IFleetTelemetryWebSocketService fleetTelemetryWebSocketService,
    IMemoryCache memoryCache,
    IBackendApiService backendApiService)
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
    private DtoFleetApiRequest SetChargeLimitRequest => new()
    {
        RequestUrl = constants.SetChargeLimitRequestUrl,
        NeedsProxy = true,
        TeslaApiRequestType = TeslaApiRequestType.Command,
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
        await WakeUpCarIfNeeded(carId).ConfigureAwait(false);

        var vin = GetVinByCarId(carId);
        await SetAmp(carId, startAmp).ConfigureAwait(false);

        var result = await SendCommandToTeslaApi<DtoVehicleCommandResult>(vin, ChargeStartRequest).ConfigureAwait(false);
    }


    public async Task WakeUpCar(int carId)
    {
        logger.LogTrace("{method}({carId})", nameof(WakeUpCar), carId);
        var car = settings.Cars.First(c => c.Id == carId);
        var result = await SendCommandToTeslaApi<DtoVehicleWakeUpResult>(car.Vin, WakeUpRequest).ConfigureAwait(false);
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
        
        var result = await SendCommandToTeslaApi<DtoVehicleCommandResult>(vin, ChargeStopRequest).ConfigureAwait(false);
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
        var result = await SendCommandToTeslaApi<DtoVehicleCommandResult>(vin, SetChargingAmpsRequest, amps).ConfigureAwait(false);
        car.LastSetAmp = amps;
    }

    public async Task<DtoValue<bool>> TestFleetApiAccess(int carId)
    {
        logger.LogTrace("{method}({carId})", nameof(TestFleetApiAccess), carId);
        var vin = GetVinByCarId(carId);
        var inMemoryCar = settings.Cars.First(c => c.Id == carId);
        try
        {
            await WakeUpCarIfNeeded(carId).ConfigureAwait(false);
            var amps = 7;
            var commandData = $"{{\"charging_amps\":{amps}}}";
            var result = await SendCommandToTeslaApi<DtoVehicleCommandResult>(vin, SetChargingAmpsRequest, amps).ConfigureAwait(false);
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
                await RefreshVehicleOnlineState(car).ConfigureAwait(false);

                if (car.State is CarStateEnum.Asleep or CarStateEnum.Offline)
                {
                    logger.LogDebug("Do not call current vehicle data as car is {state}", car.State);
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
                var vehicleData = await SendCommandToTeslaApi<DtoVehicleDataResult>(car.Vin, VehicleDataRequest)
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

    private async Task RefreshVehicleOnlineState(DtoCar car)
    {
        logger.LogTrace("{method}({carId})", nameof(RefreshVehicleOnlineState), car.Id);
        var vehicle = await SendCommandToTeslaApi<DtoVehicleResult>(car.Vin, VehicleRequest).ConfigureAwait(false);
        var vehicleResult = vehicle?.Response;
        logger.LogTrace("Got vehicle {@vehicle}", vehicle);
        if (vehicleResult == default)
        {
            await errorHandlingService.HandleError(nameof(TeslaFleetApiService), nameof(RefreshCarData), $"Error while getting vehicle info for car {car.Vin}",
                $"Could not deserialize vehicle: {JsonConvert.SerializeObject(vehicle)}", issueKeys.GetVehicle, car.Vin, null).ConfigureAwait(false);
            logger.LogError("Could not deserialize vehicle for car {carId}: {@vehicle}", car.Id, vehicle);
            return;
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

        return;
    }

    public async Task RefreshFleetApiTokenIfNeeded()
    {
        logger.LogTrace("{method}()", nameof(RefreshFleetApiTokenIfNeeded));
        var fleetApiTokenExpiration = await tokenHelper.GetFleetApiTokenExpirationDate(true);
        if (fleetApiTokenExpiration == default)
        {
            var fleetApiTokenState = await tokenHelper.GetFleetApiTokenState(true);
            logger.LogDebug("Do not refresh Fleet API Token as state is {state}", fleetApiTokenState);
            return;
        }
        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
        if(fleetApiTokenExpiration > currentDate.AddMinutes(1))
        {
            logger.LogDebug("Do not refresh Fleet API Token as it is still valid until {expiration}", fleetApiTokenExpiration);
            return;
        }
        logger.LogDebug("Refresh Fleet API Token as it is expired since {expiration}", fleetApiTokenExpiration);
        var decryptionKey = await tscConfigurationService.GetConfigurationValueByKey(constants.TeslaTokenEncryptionKeyKey);
        if (decryptionKey == default)
        {
            logger.LogError("Decryption key not found do not send command");
            throw new InvalidOperationException("No Decryption key found.");
        }
        var token = await teslaSolarChargerContext.BackendTokens.SingleOrDefaultAsync().ConfigureAwait(false);
        if (token == default)
        {
            throw new InvalidOperationException("Can not start Tesla O Auth without backend token");
        }
        var result = await backendApiService.SendRequestToBackend<DtoBackendApiTeslaResponse>(HttpMethod.Post, token.AccessToken,
            $"TeslaOAuth/RefreshToken?encryptionKey={Uri.EscapeDataString(decryptionKey)}", null);
        memoryCache.Remove(constants.FleetApiTokenStateKey);
        memoryCache.Remove(constants.FleetApiTokenExpirationTimeKey);
        if (result.HasError)
        {
            logger.LogError("Refresh Fleet API Token was not successfull. {errorMessage}", result.ErrorMessage);
            return;
        }
        var response = result.Data;
        if (response == default)
        {
            logger.LogError("Could not deserialize response from TeslaOAuth/RefreshToken");
            return;
        }

        if (response.StatusCode is >= HttpStatusCode.OK and < HttpStatusCode.Ambiguous)
        {
            logger.LogInformation("Refresh Fleet API Token was successfull");
        }
        else
        {
            logger.LogError("Refresh Token did not succeed between Backend and Tesla.");
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
        var fleetTelemetryEnabled = await teslaSolarChargerContext.Cars
            .Where(c => c.Vin == car.Vin)
            .Select(c => c.UseFleetTelemetry)
            .FirstOrDefaultAsync();
        if (fleetTelemetryEnabled)
        {
            logger.LogDebug("Do not refresh via Fleet API as Fleet Telemetry is enabled");
            return false;
            //var latestDetectedChange = latestRefresh.AddSeconds(-configurationWrapper.CarRefreshAfterCommandSeconds());
            //logger.LogTrace("Fleet Telemetry Client connected, check fleet telemetry changes and do not request Fleet API after commands.");
            //if (latestRefresh > latestChangeToDetect)
            //{
            //    logger.LogDebug("Do not refresh data for car {vin} as latest refresh is {latestRefresh} and earliest change to detect is {earliestChangeToDetect}", car.Vin, latestRefresh, latestChangeToDetect);
            //}
            //else
            //{
            //    if (await FleetTelemetryValueChanged(car.Id, CarValueType.IsCharging, latestDetectedChange, latestChangeToDetect).ConfigureAwait(false))
            //    {
            //        logger.LogDebug("Send a request as Fleet Telemetry detected a change in is charging state.");
            //        return true;
            //    }

            //    if (await FleetTelemetryValueChanged(car.Id, CarValueType.IsPluggedIn, latestDetectedChange, latestChangeToDetect).ConfigureAwait(false))
            //    {
            //        logger.LogDebug("Send a request as Fleet Telemetry detected a change in plugged in state.");
            //        return true;
            //    }

            //    var latestValueBeforeLatestRefresh = await GetLatestValueBeforeTimeStamp(car.Id, CarValueType.ChargeAmps, latestDetectedChange).ConfigureAwait(false);
            //    var latestValue = await GetLatestValueBeforeTimeStamp(car.Id, CarValueType.ChargeAmps, latestChangeToDetect).ConfigureAwait(false);
            //    if (latestValue != default && latestValueBeforeLatestRefresh == default)
            //    {
            //        logger.LogDebug("Send a request as the first charging amps value was detected.");
            //        return true;
            //    }

            //    if (latestValue != default && latestValueBeforeLatestRefresh != default)
            //    {
            //        List<CarValueLogTimeStampAndValues> values =
            //        [
            //            latestValue,
            //            latestValueBeforeLatestRefresh,
            //        ];
            //        if (AnyValueChanged(values) && values.Any(v => v.DoubleValue == 0))
            //        {
            //            logger.LogDebug("Send a request as Fleet Telemetry detected at least one 0 value in charging amps.");
            //            return true;
            //        }
            //    }

            //}

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

    private async Task WakeUpCarIfNeeded(int carId)
    {
        logger.LogTrace("{method}({carId})", nameof(WakeUpCarIfNeeded), carId);
        var car = settings.Cars.First(c => c.Id == carId);
        await RefreshVehicleOnlineState(car);
        if (car.State is not (CarStateEnum.Asleep or CarStateEnum.Offline or CarStateEnum.Suspended))
        {
            return;
        }
        switch (car.State)
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

    private async Task<DtoGenericTeslaResponse<T>?> SendCommandToTeslaApi<T>(string vin, DtoFleetApiRequest fleetApiRequest, int? intParam = null) where T : class
    {
        logger.LogTrace("{method}({vin}, {@fleetApiRequest}, {intParam})", nameof(SendCommandToTeslaApi), vin, fleetApiRequest, intParam);
        if (await tokenHelper.GetBackendTokenState(true) != TokenState.UpToDate)
        {
            //Do not show base api not licensed error if not connected to backend
            await errorHandlingService.HandleErrorResolved(issueKeys.BaseAppNotLicensed, null);
            return null;
        }
        if (!await backendApiService.IsBaseAppLicensed(true))
        {
            logger.LogError("Can not send request to car as base app is not licensed");
            await errorHandlingService.HandleError(nameof(TeslaFleetApiService), nameof(SendCommandToTeslaApi), "Base App not licensed",
                "Can not send commands to car as app is not licensed. Buy a subscription on <a href=\"https://solar4car.com/subscriptions\">Solar4Car Subscriptions</a> to use TSC",
                issueKeys.BaseAppNotLicensed, null, null).ConfigureAwait(false);
            return null;
        }
        await errorHandlingService.HandleErrorResolved(issueKeys.BaseAppNotLicensed, null);

        var car = settings.Cars.First(c => c.Vin == vin);
        if (fleetApiRequest.BleCompatible)
        {
            
            var isCarBleEnabled = car.UseBle;
            if (isCarBleEnabled)
            {
                await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiNotLicensed, car.Vin);
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
                            result = await bleService.SetAmp(vin, intParam!.Value);
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
                    if (await backendApiService.IsFleetApiLicensed(car.Vin, true))
                    {
                        car.LastNonSuccessBleCall = dateTimeProvider.UtcNow();
                        var fallbackUntilLocalTimeString =
                            (car.LastNonSuccessBleCall + configurationWrapper.BleUsageStopAfterError()).Value.ToLocalTime();
                        logger.LogWarning("Command BLE enabled but command did not succeed, using Fleet API as fallback until {fallbackUntil}.", fallbackUntilLocalTimeString);
                        await errorHandlingService.HandleError(nameof(TeslaFleetApiService), nameof(SendCommandToTeslaApi),
                            $"Using Fleet API as BLE fallback for car {car.Vin}",
                            $"As the BLE command did not succeed, Fleet API is used as fallback until {fallbackUntilLocalTimeString}. Note: During this time it is not possible to retry BLE automatically you need to go to the car settings page and test BLE access manually.",
                            issueKeys.UsingFleetApiAsBleFallback, car.Vin, null).ConfigureAwait(false);
                    }
                    else
                    {
                        //Do not use Fleet API if not licensed
                        logger.LogInformation("Do not use Fleet API as Fallback as Fleet API is not licensed for car {vin}", car.Vin);
                        return null;
                    }
                    
                }
                
            }
        }

        if (fleetApiRequest.RequestUrl != VehicleRequest.RequestUrl && (!await backendApiService.IsFleetApiLicensed(car.Vin, true)))
        {
            await errorHandlingService.HandleError(nameof(TeslaFleetApiService), nameof(SendCommandToTeslaApi), $"Fleet API not licensed for car {car.Vin}",
                "Can not send Fleet API commands to car as Fleet API is not licensed",
                issueKeys.FleetApiNotLicensed, car.Vin, null).ConfigureAwait(false);

            logger.LogError("Can not send Fleet API commands to car {vin} as car is not licensed", car.Vin);
            return null;
        }
        await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiNotLicensed, car.Vin);

        var accessToken = await teslaSolarChargerContext.BackendTokens.SingleOrDefaultAsync();
        if (accessToken == default)
        {
            logger.LogError("Access token not found do not send command");
            return null;
        }
        var fleetApiProxyRequired = await IsFleetApiProxyEnabled(vin).ConfigureAwait(false);
        var decryptionKey = await tscConfigurationService.GetConfigurationValueByKey(constants.TeslaTokenEncryptionKeyKey);
        if (decryptionKey == default)
        {
            logger.LogError("Decryption key not found do not send command");
            return null;
        }
        var requestUri = $"{fleetApiRequest.RequestUrl}?encryptionKey={Uri.EscapeDataString(decryptionKey)}&vin={vin}&carRequiresProxy={fleetApiProxyRequired.Value}";
        if (fleetApiRequest.RequestUrl == SetChargingAmpsRequest.RequestUrl)
        {
            requestUri += $"&amps={intParam}";
        }
        var backendResult = await backendApiService.SendRequestToBackend<DtoBackendApiTeslaResponse>(HttpMethod.Post, accessToken.AccessToken, requestUri, null).ConfigureAwait(false);
        if (backendResult.HasError)
        {
            await errorHandlingService.HandleError(nameof(TeslaFleetApiService), nameof(SendCommandToTeslaApi), $"Solar4Car related error while sending command to car {car.Vin}",
                $"Sending command to Tesla API resulted in non succes status code. The issue very likely is not on Tesla's side but on Solar4Car side. Error Message: {backendResult.ErrorMessage} : Command name:{fleetApiRequest.RequestUrl}, Int Param:{intParam}.",
                issueKeys.Solar4CarSideFleetApiNonSuccessStatusCode + fleetApiRequest.RequestUrl, car.Vin, null).ConfigureAwait(false);
            logger.LogError("Sending command to Tesla API resulted in non succes status code. The issue very likely is not on Tesla's side but on Solar4Car side. ErrorMessage: {errorMessage} : Command name:{commandName}, Int Param:{intParam}.", backendResult.ErrorMessage, fleetApiRequest.RequestUrl, intParam);
            return null;
        }
        else
        {
            await errorHandlingService.HandleErrorResolved(issueKeys.Solar4CarSideFleetApiNonSuccessStatusCode + fleetApiRequest.RequestUrl, car.Vin);
        }
        var backendApiResponse = backendResult.Data;
        if (backendApiResponse == default)
        {
            logger.LogError("Could not deserialize Backend API Tesla Response.");
            return null;
        }

        if (backendApiResponse.StatusCode is >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices)
        {
            AddRequestToCar(vin, fleetApiRequest);
            await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiNonSuccessStatusCode + fleetApiRequest.RequestUrl, car.Vin);
        }
        else
        {
            await errorHandlingService.HandleError(nameof(TeslaFleetApiService), nameof(SendCommandToTeslaApi), $"Tesla related error while sending command to car {car.Vin}",
                $"Sending command to Tesla API resulted in non succes status code: {backendApiResponse.StatusCode} : Command name:{fleetApiRequest.RequestUrl}, Int Param:{intParam}. Tesla Result: {backendApiResponse.JsonResponse}",
                issueKeys.FleetApiNonSuccessStatusCode + fleetApiRequest.RequestUrl, car.Vin, null).ConfigureAwait(false);
            logger.LogError("Sending command to Tesla API resulted in non succes status code: {statusCode} : Command name:{commandName}, Int Param:{intParam}. Tesla Result: {result}", backendApiResponse.StatusCode, fleetApiRequest.RequestUrl, intParam, backendApiResponse.JsonResponse);
            //ToDo: should be able to handle null backend API response. e.g. with an error "incompatible version".
            await HandleNonSuccessTeslaApiStatusCodes(backendApiResponse.StatusCode, backendApiResponse.JsonResponse, vin).ConfigureAwait(false);
            return null;
        }


        //ToDo: should be able to handle null backend API response. e.g. with an error "incompatible version".
        var teslaCommandResultResponse = JsonConvert.DeserializeObject<DtoGenericTeslaResponse<T>>(backendApiResponse.JsonResponse);
        if ((backendApiResponse.StatusCode is >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices) && (teslaCommandResultResponse?.Response is DtoVehicleCommandResult vehicleCommandResult))
        {
            if (vehicleCommandResult.Result != true
                && !((fleetApiRequest.RequestUrl == ChargeStartRequest.RequestUrl) && backendApiResponse.JsonResponse.Contains(IsChargingErrorMessage))
                && !((fleetApiRequest.RequestUrl == ChargeStopRequest.RequestUrl) && backendApiResponse.JsonResponse.Contains(IsNotChargingErrorMessage)))
            {
                await errorHandlingService.HandleError(nameof(TeslaFleetApiService), nameof(SendCommandToTeslaApi), $"Car {car.Vin} could not handle command",
                        $"Result of command request is false {fleetApiRequest.RequestUrl}, {intParam}. Response string: {backendApiResponse.JsonResponse}",
                        issueKeys.FleetApiNonSuccessResult + fleetApiRequest.RequestUrl, car.Vin, null)
                    .ConfigureAwait(false);
                logger.LogError("Result of command request is false {fleetApiRequest.RequestUrl}, {intParam}. Response string: {responseString}", fleetApiRequest.RequestUrl, intParam, backendApiResponse.JsonResponse);
                await HandleUnsignedCommands(vehicleCommandResult, vin).ConfigureAwait(false);
            }
            else
            {
                await errorHandlingService.HandleErrorResolved(issueKeys.FleetApiNonSuccessResult + fleetApiRequest.RequestUrl, car.Vin);
            }
        }
        logger.LogDebug("Response: {responseString}", backendApiResponse.JsonResponse);
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
        else if (fleetApiRequest.RequestUrl == SetChargeLimitRequest.RequestUrl)
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

    public async Task RefreshFleetApiRequestsAreAllowed()
    {
        logger.LogTrace("{method}()", nameof(RefreshFleetApiRequestsAreAllowed));
        //Currently no server side implmenetation
        //if (settings.AllowUnlimitedFleetApiRequests && (settings.LastFleetApiRequestAllowedCheck > dateTimeProvider.UtcNow().AddHours(-1)))
        //{
        //    return;
        //}
        //settings.LastFleetApiRequestAllowedCheck = dateTimeProvider.UtcNow();
        //using var httpClient = new HttpClient();
        //httpClient.Timeout = TimeSpan.FromSeconds(2);
        //var installationId = await tscConfigurationService.GetInstallationId().ConfigureAwait(false);
        //var url = configurationWrapper.BackendApiBaseUrl() + $"Tsc/AllowUnlimitedFleetApiAccess?installationId={installationId}";
        //try
        //{
        //    var response = await httpClient.GetAsync(url).ConfigureAwait(false);
        //    var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        //    if (!response.IsSuccessStatusCode)
        //    {
        //        settings.AllowUnlimitedFleetApiRequests = true;
        //        return;
        //    }

        //    var responseValue = JsonConvert.DeserializeObject<DtoValue<bool>>(responseString);
        //    settings.AllowUnlimitedFleetApiRequests = responseValue?.Value == true;
        //}
        //catch (Exception)
        //{
        //    settings.AllowUnlimitedFleetApiRequests = false;
        //}
        
    }

    public async Task<DtoValue<TokenState>> GetFleetApiTokenState(bool useCache)
    {
        var tokenState = await tokenHelper.GetFleetApiTokenState(useCache);
        return new(tokenState);
    }

    private async Task HandleNonSuccessTeslaApiStatusCodes(HttpStatusCode statusCode,
        string responseString, string? vin = null)
    {
        logger.LogTrace("{method}({statusCode}, {responseString})", nameof(HandleNonSuccessTeslaApiStatusCodes), statusCode, responseString);
        if (statusCode == HttpStatusCode.Unauthorized)
        {
            logger.LogError(
                "Your token or refresh token is invalid. Response: {responseString}", responseString);
            await tscConfigurationService.SetConfigurationValueByKey(constants.FleetApiTokenUnauthorizedKey, "true");
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
                await tscConfigurationService.SetConfigurationValueByKey(constants.FleetApiTokenMissingScopes, "true");
            }
            
        }
        else if (statusCode == HttpStatusCode.InternalServerError
                 && responseString.Contains("vehicle rejected request: your public key has not been paired with the vehicle"))
        {
            logger.LogError("Vehicle {vin} is not paired with TSC. Add The public key to the vehicle. Response: {responseString}", vin, responseString);
            var car = teslaSolarChargerContext.Cars.First(c => c.Vin == vin);
            car.TeslaFleetApiState = TeslaCarFleetApiState.NotWorking;
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
        var accessToken = await teslaSolarChargerContext.BackendTokens.SingleOrDefaultAsync();
        if (accessToken == default)
        {
            logger.LogError("Can not add cars to TSC as no Backend Token was found");
            return Fin<List<DtoTesla>>.Fail("No Backend token found or existing token expired.");
        }
        var decryptionKey = await tscConfigurationService.GetConfigurationValueByKey(constants.TeslaTokenEncryptionKeyKey);
        if (decryptionKey == default)
        {
            logger.LogError("Decryption key not found do not send command");
            return Fin<List<DtoTesla>>.Fail("No Decryption key found.");
        }
        var requestUri = $"FleetApiRequests/GetAllCarsFromAccount?encryptionKey={Uri.EscapeDataString(decryptionKey)}";
        try
        {
            var backendResponse = await backendApiService.SendRequestToBackend<DtoBackendApiTeslaResponse>(HttpMethod.Get,
                accessToken.AccessToken, requestUri, null);
            if (backendResponse.HasError)
            {
                logger.LogError("Error while getting all cars from account: {errorMessage}", backendResponse.ErrorMessage);
                var exception = new HttpRequestException($"Requesting {requestUri} returned following error: {backendResponse.ErrorMessage}", null);
                return Fin<List<DtoTesla>>.Fail(Error.New(exception));
            }

            var teslaBackendResult = backendResponse.Data;
            if (teslaBackendResult == null)
            {
                logger.LogError("Could not deserialize Solar4CarBackend response body");
                return Fin<List<DtoTesla>>.Fail($"Could not deserialize response body");
            }

            if (teslaBackendResult.StatusCode is >= HttpStatusCode.OK and < HttpStatusCode.MultipleChoices)
            {
                logger.LogError("Error while getting all cars from account due to communication issue between Solar4Car Backend and Tesla: Underlaying Status code: {statusCode}; Underlaying Result: {jsonResult}", teslaBackendResult.StatusCode, teslaBackendResult.JsonResponse);
                var excpetion = new HttpRequestException($"Requesting {requestUri} returned following statusCode: {teslaBackendResult.StatusCode} Underlaying result: {teslaBackendResult.JsonResponse}", null,
                    teslaBackendResult.StatusCode);
                return Fin<List<DtoTesla>>.Fail(Error.New(excpetion));
            }

            if(string.IsNullOrWhiteSpace(teslaBackendResult.JsonResponse))
            {
                logger.LogError("Empty Tesla JSON response body from Solar4Car Backend");
                return Fin<List<DtoTesla>>.Fail("Empty Tesla JSON response body from Solar4Car Backend");
            }

            var vehicles = JsonConvert.DeserializeObject<DtoGenericTeslaResponse<List<DtoVehicleResult>>>(teslaBackendResult.JsonResponse);

            if (vehicles?.Response == null)
            {
                logger.LogError("Could not deserialize vehicle list response body");
                return Fin<List<DtoTesla>>.Fail($"Could not deserialize response body");
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
