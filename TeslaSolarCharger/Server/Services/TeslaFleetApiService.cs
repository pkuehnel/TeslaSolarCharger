using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Dtos.TeslaFleetApi;
using TeslaSolarCharger.Server.Dtos.TscBackend;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Ble;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedBackend.Contracts;
using TeslaSolarCharger.SharedBackend.Dtos;

namespace TeslaSolarCharger.Server.Services;

public class TeslaFleetApiService(
    ILogger<TeslaFleetApiService> logger,
    ITeslaSolarChargerContext teslaSolarChargerContext,
    IDateTimeProvider dateTimeProvider,
    ITeslamateContext teslamateContext,
    IConfigurationWrapper configurationWrapper,
    ITeslamateApiService teslamateApiService,
    IConstants constants,
    ITscConfigurationService tscConfigurationService,
    IBackendApiService backendApiService,
    ISettings settings,
    IConfigJsonService configJsonService,
    IBleService bleService)
    : ITeslaService, ITeslaFleetApiService
{
    private DtoFleetApiRequest ChargeStartRequest => new()
    {
        RequestUrl = "command/charge_start",
        NeedsProxy = true,
        BleCompatible = true,
    };
    private DtoFleetApiRequest ChargeStopRequest => new()
    {
        RequestUrl = "command/charge_stop",
        NeedsProxy = true,
        BleCompatible = true,
    };
    private DtoFleetApiRequest SetChargingAmpsRequest => new()
    {
        RequestUrl = "command/set_charging_amps",
        NeedsProxy = true,
        BleCompatible = true,
    };
    private DtoFleetApiRequest SetScheduledChargingRequest => new()
    {
        RequestUrl = "command/set_scheduled_charging",
        NeedsProxy = true,
    };
    private DtoFleetApiRequest SetChargeLimitRequest => new()
    {
        RequestUrl = "command/set_charge_limit",
        NeedsProxy = true,
    };
    private DtoFleetApiRequest OpenChargePortDoorRequest => new()
    {
        RequestUrl = "command/charge_port_door_open",
        NeedsProxy = true,
    };
    private DtoFleetApiRequest WakeUpRequest => new()
    {
        RequestUrl = "wake_up",
        NeedsProxy = false,
    };

    private DtoFleetApiRequest VehicleRequest => new()
    {
        RequestUrl = "",
        NeedsProxy = false,
    };

    private DtoFleetApiRequest VehicleDataRequest => new()
    {
        RequestUrl = $"vehicle_data?endpoints={Uri.EscapeDataString("drive_state;location_data;vehicle_state;charge_state;climate_state")}",
        NeedsProxy = false,
    };

    public async Task StartCharging(int carId, int startAmp, CarStateEnum? carState)
    {
        logger.LogTrace("{method}({carId}, {startAmp}, {carState})", nameof(StartCharging), carId, startAmp, carState);
        if (startAmp == 0)
        {
            logger.LogDebug("Should start charging with 0 amp. Skipping charge start.");
            return;
        }
        await WakeUpCarIfNeeded(carId, carState).ConfigureAwait(false);

        var vin = GetVinByCarId(carId);
        await SetAmp(carId, startAmp).ConfigureAwait(false);

        var result = await SendCommandToTeslaApi<DtoVehicleCommandResult>(vin, ChargeStartRequest, HttpMethod.Post).ConfigureAwait(false);
        if (result?.Response?.Result == true && configurationWrapper.GetVehicleDataFromTesla())
        {
            var car = settings.Cars.First(c => c.Id == carId);
            car.State = CarStateEnum.Charging;
            car.ChargerActualCurrent = startAmp;
            car.ChargerVoltage = settings.AverageHomeGridVoltage ?? 230;
        }
    }


    public async Task WakeUpCar(int carId)
    {
        logger.LogTrace("{method}({carId})", nameof(WakeUpCar), carId);
        var vin = GetVinByCarId(carId);
        var result = await SendCommandToTeslaApi<DtoVehicleWakeUpResult>(vin, WakeUpRequest, HttpMethod.Post).ConfigureAwait(false);
        await teslamateApiService.ResumeLogging(carId).ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromSeconds(20)).ConfigureAwait(false);
        var car = settings.Cars.First(c => c.Id == carId);
        car.State = CarStateEnum.Online;
    }

    public async Task StopCharging(int carId)
    {
        logger.LogTrace("{method}({carId})", nameof(StopCharging), carId);
        var vin = GetVinByCarId(carId);
        var result = await SendCommandToTeslaApi<DtoVehicleCommandResult>(vin, ChargeStopRequest, HttpMethod.Post).ConfigureAwait(false);
        if (result?.Response?.Result == true && configurationWrapper.GetVehicleDataFromTesla())
        {
            var car = settings.Cars.First(c => c.Id == carId);
            car.State = CarStateEnum.Online;
            car.ChargerActualCurrent = 0;
        }
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
        if (result?.Response?.Result == true && configurationWrapper.GetVehicleDataFromTesla())
        {
            car.ChargerRequestedCurrent = amps;
            car.ChargerActualCurrent = car.State == CarStateEnum.Charging ? amps : 0;
        }
        
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

    public async Task<DtoValue<bool>> TestFleetApiAccess(int carId)
    {
        logger.LogTrace("{method}({carId})", nameof(TestFleetApiAccess), carId);
        var vin = GetVinByCarId(carId);
        var inMemoryCar = settings.Cars.First(c => c.Id == carId);
        try
        {
            await WakeUpCarIfNeeded(carId, inMemoryCar.State).ConfigureAwait(false);
            var result = await SendCommandToTeslaApi<DtoVehicleCommandResult>(vin, OpenChargePortDoorRequest, HttpMethod.Post).ConfigureAwait(false);
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

    public DtoValue<bool> IsFleetApiEnabled()
    {
        logger.LogTrace("{method}", nameof(IsFleetApiEnabled));
        var isEnabled = configurationWrapper.UseFleetApi();
        return new DtoValue<bool>(isEnabled);
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

    public async Task OpenChargePortDoor(int carId)
    {
        logger.LogTrace("{method}({carId})", nameof(OpenChargePortDoor), carId);
        var vin = GetVinByCarId(carId);
        var result = await SendCommandToTeslaApi<DtoVehicleCommandResult>(vin, OpenChargePortDoorRequest, HttpMethod.Post).ConfigureAwait(false);
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
            var currentUtcDate = dateTimeProvider.DateTimeOffSetUtcNow();
            if (car.LastApiDataRefresh.AddSeconds(car.ApiRefreshIntervalSeconds) > currentUtcDate)
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
                    await backendApiService.PostErrorInformation(nameof(TeslaFleetApiService), nameof(RefreshCarData),
                                               $"Could not deserialize vehicle: {JsonConvert.SerializeObject(vehicle)}").ConfigureAwait(false);
                    logger.LogError("Could not deserialize vehicle for car {carId}: {@vehicle}", carId, vehicle);
                    continue;
                }
                var vehicleState = vehicleResult.State;
                if (configurationWrapper.GetVehicleDataFromTesla())
                {
                    if (vehicleState == "asleep")
                    {
                        car.State = CarStateEnum.Asleep;
                    }
                    else if (vehicleState == "offline")
                    {
                        car.State = CarStateEnum.Offline;
                    }
                }

                if (vehicleState is "asleep" or "offline")
                {
                    logger.LogDebug("Do not call current vehicle data as car is {state}", vehicleState);
                    continue;
                }
                var vehicleData = await SendCommandToTeslaApi<DtoVehicleDataResult>(car.Vin, VehicleDataRequest, HttpMethod.Get)
                    .ConfigureAwait(false);
                car.LastApiDataRefresh = currentUtcDate;
                logger.LogTrace("Got vehicleData {@vehicleData}", vehicleData);
                var vehicleDataResult = vehicleData?.Response;
                if (vehicleData?.Error?.Contains("offline") == true)
                {
                    car.State = CarStateEnum.Offline;
                    car.ChargerActualCurrent = 0;
                }
                if (vehicleDataResult == default)
                {
                    await backendApiService.PostErrorInformation(nameof(TeslaFleetApiService), nameof(RefreshCarData),
                        $"Could not deserialize vehicle data: {JsonConvert.SerializeObject(vehicleData)}").ConfigureAwait(false);
                    logger.LogError("Could not deserialize vehicle data for car {carId}: {@vehicleData}", carId, vehicleData);
                    continue;
                }

                if (configurationWrapper.GetVehicleDataFromTesla())
                {
                    
                    car.Name = vehicleDataResult.VehicleState.VehicleName;
                    car.SoC = vehicleDataResult.ChargeState.BatteryLevel;
                    car.SocLimit = vehicleDataResult.ChargeState.ChargeLimitSoc;
                    var minimumSettableSocLimit = vehicleDataResult.ChargeState.ChargeLimitSocMin;
                    if (car.MinimumSoC > car.SocLimit && car.SocLimit > minimumSettableSocLimit)
                    {
                        logger.LogWarning("Reduce Minimum SoC {minimumSoC} as charge limit {chargeLimit} is lower.", car.MinimumSoC, car.SocLimit);
                        car.MinimumSoC = (int)car.SocLimit;
                        logger.LogError("Can not handle lower Soc than minimumSoc");
                    }
                    car.ChargerPhases = vehicleDataResult.ChargeState.ChargerPhases;
                    car.ChargerVoltage = vehicleDataResult.ChargeState.ChargerVoltage;
                    car.ChargerActualCurrent = vehicleDataResult.ChargeState.ChargerActualCurrent;
                    car.PluggedIn = vehicleDataResult.ChargeState.ChargingState != "Disconnected";
                    car.ClimateOn = vehicleDataResult.ClimateState.IsClimateOn;
                    car.TimeUntilFullCharge = TimeSpan.FromHours(vehicleDataResult.ChargeState.TimeToFullCharge);
                    var teslaCarStateString = vehicleDataResult.State;
                    var teslaCarShiftState = vehicleDataResult.DriveState.ShiftState;
                    var teslaCarSoftwareUpdateState = vehicleDataResult.VehicleState.SoftwareUpdate.Status;
                    var chargingState = vehicleDataResult.ChargeState.ChargingState;
                    car.State = DetermineCarState(teslaCarStateString, teslaCarShiftState, teslaCarSoftwareUpdateState, chargingState);
                    if (car.State == CarStateEnum.Unknown)
                    {
                        await backendApiService.PostErrorInformation(nameof(TeslaFleetApiService), nameof(RefreshCarData),
                            $"Could not determine car state. TeslaCarStateString: {teslaCarStateString}, TeslaCarShiftState: {teslaCarShiftState}, TeslaCarSoftwareUpdateState: {teslaCarSoftwareUpdateState}, ChargingState: {chargingState}").ConfigureAwait(false);
                    }
                    car.Healthy = true;
                    car.ChargerRequestedCurrent = vehicleDataResult.ChargeState.ChargeCurrentRequest;
                    car.ChargerPilotCurrent = vehicleDataResult.ChargeState.ChargerPilotCurrent;
                    car.ScheduledChargingStartTime = vehicleDataResult.ChargeState.ScheduledChargingStartTime == null ? (DateTimeOffset?)null : DateTimeOffset.FromUnixTimeSeconds(vehicleDataResult.ChargeState.ScheduledChargingStartTime.Value);
                    car.Longitude = vehicleDataResult.DriveState.Longitude;
                    car.Latitude = vehicleDataResult.DriveState.Latitude;
                }
                

            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not get vehicle data for car {carId}", carId);
                await backendApiService.PostErrorInformation(nameof(TeslaFleetApiService), nameof(RefreshCarData),
                    $"Error getting vehicle data: {ex.Message} {ex.StackTrace}").ConfigureAwait(false);
            }
        }
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
        switch (carState)
        {
            case CarStateEnum.Offline or CarStateEnum.Asleep:
                logger.LogInformation("Wakeup car.");
                await WakeUpCar(carId).ConfigureAwait(false);
                break;
            case CarStateEnum.Suspended:
                logger.LogInformation("Resume logging as is suspended");
                await teslamateApiService.ResumeLogging(carId).ConfigureAwait(false);
                break;
        }
    }

    private async Task<DtoGenericTeslaResponse<T>?> SendCommandToTeslaApi<T>(string vin, DtoFleetApiRequest fleetApiRequest, HttpMethod httpMethod, string contentData = "{}", int? amp = null) where T : class
    {
        logger.LogTrace("{method}({vin}, {@fleetApiRequest}, {contentData})", nameof(SendCommandToTeslaApi), vin, fleetApiRequest, contentData);
        AddRequestToCar(vin, fleetApiRequest);
        if (fleetApiRequest.BleCompatible)
        {
            var car = settings.Cars.First(c => c.Vin == vin);
            var isCarBleEnabled = car.UseBle;
            if (isCarBleEnabled)
            {
                
                var result = new DtoBleResult();
                if (fleetApiRequest.RequestUrl == ChargeStartRequest.RequestUrl)
                {
                    result = await bleService.StartCharging(vin);
                    if (result.Success && configurationWrapper.GetVehicleDataFromTesla())
                    {
                        car.State = CarStateEnum.Charging;
                        car.ChargerActualCurrent = car.ChargerRequestedCurrent;
                        car.ChargerVoltage = settings.AverageHomeGridVoltage ?? 230;
                    }
                }
                else if (fleetApiRequest.RequestUrl == ChargeStopRequest.RequestUrl)
                {
                    result = await bleService.StopCharging(vin);
                    if (result.Success && configurationWrapper.GetVehicleDataFromTesla())
                    {
                        car.State = CarStateEnum.Online;
                        car.ChargerActualCurrent = 0;
                    }
                }
                else if (fleetApiRequest.RequestUrl == SetChargingAmpsRequest.RequestUrl)
                {
                    result = await bleService.SetAmp(vin, amp!.Value);
                    if (result.Success && configurationWrapper.GetVehicleDataFromTesla())
                    {
                        car.ChargerRequestedCurrent = amp!.Value;
                        car.ChargerActualCurrent = car.State == CarStateEnum.Charging ? amp!.Value : 0;
                    }
                }

                if (typeof(T) == typeof(DtoVehicleCommandResult))
                {
                    var comamndResult = new DtoGenericTeslaResponse<T>() { };
                    comamndResult.Response = (T)(object) new DtoVehicleCommandResult()
                    {
                        Result = result.Success,
                        Reason = result.Message,
                    };
                    return comamndResult;
                }

                return new DtoGenericTeslaResponse<T>();

                
            }
        }
        var accessToken = await GetAccessToken().ConfigureAwait(false);
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.AccessToken);
        var content = new StringContent(contentData, System.Text.Encoding.UTF8, "application/json");
        var rateLimitedUntil = await RateLimitedUntil(vin).ConfigureAwait(false);
        var currentDate = dateTimeProvider.UtcNow();
        if (currentDate < rateLimitedUntil)
        {
            logger.LogError("Car with VIN {vin} rate limited until {rateLimitedUntil}. Skipping command.", vin, rateLimitedUntil);
            return null;
        }
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
        }
        else
        {
            await backendApiService.PostErrorInformation(nameof(TeslaFleetApiService), nameof(SendCommandToTeslaApi),
                $"Sending command to Tesla API resulted in non succes status code: {response.StatusCode} : Command name:{fleetApiRequest.RequestUrl}, Content data:{contentData}. Response string: {responseString}").ConfigureAwait(false);
            logger.LogError("Sending command to Tesla API resulted in non succes status code: {statusCode} : Command name:{commandName}, Content data:{contentData}. Response string: {responseString}", response.StatusCode, fleetApiRequest.RequestUrl, contentData, responseString);
            await HandleNonSuccessTeslaApiStatusCodes(response.StatusCode, accessToken, responseString, vin).ConfigureAwait(false);
        }
        var teslaCommandResultResponse = JsonConvert.DeserializeObject<DtoGenericTeslaResponse<T>>(responseString);
        if (response.IsSuccessStatusCode && (teslaCommandResultResponse?.Response is DtoVehicleCommandResult vehicleCommandResult))
        {
            if (vehicleCommandResult.Result != true)
            {
                await backendApiService.PostErrorInformation(nameof(TeslaFleetApiService), nameof(SendCommandToTeslaApi),
                        $"Result of command request is false {fleetApiRequest.RequestUrl}, {contentData}. Response string: {responseString}")
                    .ConfigureAwait(false);
                logger.LogError("Result of command request is false {fleetApiRequest.RequestUrl}, {contentData}. Response string: {responseString}", fleetApiRequest.RequestUrl, contentData, responseString);
                await HandleUnsignedCommands(vehicleCommandResult, vin).ConfigureAwait(false);
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
        else if (fleetApiRequest.RequestUrl == OpenChargePortDoorRequest.RequestUrl)
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

    private async Task<DateTime?> RateLimitedUntil(string vin)
    {
        logger.LogTrace("{method}", nameof(RateLimitedUntil));
        var rateLimitedUntil = await teslaSolarChargerContext.Cars
            .Where(c => c.Vin == vin)
            .Select(c => c.RateLimitedUntil)
            .FirstAsync();
        return rateLimitedUntil;
    }

    private async Task HandleUnsignedCommands(DtoVehicleCommandResult vehicleCommandResult, string vin)
    {
        if (string.Equals(vehicleCommandResult.Reason, "unsigned_cmds_hardlocked"))
        {
            await backendApiService.PostErrorInformation(nameof(TeslaFleetApiService), nameof(SendCommandToTeslaApi),
                    "FleetAPI proxy needed set to true")
                .ConfigureAwait(false);
            if (!(await IsFleetApiProxyEnabled(vin).ConfigureAwait(false)).Value)
            {
                var car = teslaSolarChargerContext.Cars.First(c => c.Vin == vin);
                car.VehicleCommandProtocolRequired = true;
                await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
            }
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

    public async Task GetNewTokenFromBackend()
    {
        logger.LogTrace("{method}()", nameof(GetNewTokenFromBackend));
        //As all tokens get deleted when requesting a new one, we can assume that there is no token in the database.
        var token = await teslaSolarChargerContext.TeslaTokens.FirstOrDefaultAsync().ConfigureAwait(false);
        if (token == null)
        {
            var tokenRequestedDate = await GetTokenRequestedDate().ConfigureAwait(false);
            if (tokenRequestedDate == null)
            {
                logger.LogError("Token has not been requested. Fleet API currently not working");
                return;
            }
            if (tokenRequestedDate < dateTimeProvider.UtcNow().Subtract(constants.MaxTokenRequestWaitTime))
            {
                logger.LogError("Last token request is too old. Request a new token.");
                return;
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
            }
            else
            {
                var newToken = JsonConvert.DeserializeObject<DtoTeslaTscDeliveryToken>(responseString) ?? throw new InvalidDataException("Could not get token from string.");
                await AddNewTokenAsync(newToken).ConfigureAwait(false);
            }
            
        }
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
        //ToDo: needs to handle manual generated tokens. For now as soon as rate limits are introduced nobody gets refresh tokens even if they have a token not from www.teslasolarcharger.de
        if (settings.AllowUnlimitedFleetApiRequests == false)
        {
            logger.LogError("Due to rate limitations fleet api requests are not allowed. As this version can not handle rate limits try updating to the latest version.");
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
            }
            else
            {
                await backendApiService.PostErrorInformation(nameof(TeslaFleetApiService), nameof(SendCommandToTeslaApi),
                    $"Refreshing token did result in non success status code. Response status code: {response.StatusCode} Response string: {responseString}").ConfigureAwait(false);
                logger.LogError("Refreshing token did result in non success status code. Response status code: {statusCode} Response string: {responseString}", response.StatusCode, responseString);
                await HandleNonSuccessTeslaApiStatusCodes(response.StatusCode, tokenToRefresh, responseString).ConfigureAwait(false);
            }
            response.EnsureSuccessStatusCode();
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
            settings.AllowUnlimitedFleetApiRequests = responseValue?.Value != false;
        }
        catch (Exception)
        {
            settings.AllowUnlimitedFleetApiRequests = true;
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
        if (!configurationWrapper.UseFleetApi())
        {
            return new DtoValue<FleetApiTokenState>(FleetApiTokenState.NotNeeded);
        }

        if (!settings.AllowUnlimitedFleetApiRequests)
        {
            return new DtoValue<FleetApiTokenState>(FleetApiTokenState.NoApiRequestsAllowed);
        }
        var hasCurrentTokenMissingScopes = await teslaSolarChargerContext.TscConfigurations
            .Where(c => c.Key == constants.TokenMissingScopes)
            .AnyAsync().ConfigureAwait(false);
        if (hasCurrentTokenMissingScopes)
        {
            return new DtoValue<FleetApiTokenState>(FleetApiTokenState.MissingScopes);
        }
        var token = await teslaSolarChargerContext.TeslaTokens.FirstOrDefaultAsync().ConfigureAwait(false);
        if (token != null)
        {
            if (token.UnauthorizedCounter > constants.MaxTokenUnauthorizedCount)
            {
                return new DtoValue<FleetApiTokenState>(FleetApiTokenState.TokenUnauthorized);
            }
            return new DtoValue<FleetApiTokenState>(token.ExpiresAtUtc < dateTimeProvider.UtcNow() ? FleetApiTokenState.Expired : FleetApiTokenState.UpToDate);
        }
        var tokenRequestedDate = await GetTokenRequestedDate().ConfigureAwait(false);
        if (tokenRequestedDate == null)
        {
            return new DtoValue<FleetApiTokenState>(FleetApiTokenState.NotRequested);
        }
        var currentDate = dateTimeProvider.UtcNow();
        if (tokenRequestedDate < (currentDate - constants.MaxTokenRequestWaitTime))
        {
            return new DtoValue<FleetApiTokenState>(FleetApiTokenState.TokenRequestExpired);
        }
        return new DtoValue<FleetApiTokenState>(FleetApiTokenState.NotReceived);
    }

    private async Task<DateTime?> GetTokenRequestedDate()
    {
        var tokenRequestedDateString = await teslaSolarChargerContext.TscConfigurations
            .Where(c => c.Key == constants.FleetApiTokenRequested)
            .Select(c => c.Value)
            .FirstOrDefaultAsync().ConfigureAwait(false);
        if (tokenRequestedDateString == null)
        {
            return null;
        }
        var tokenRequestedDate = DateTime.Parse(tokenRequestedDateString, null, DateTimeStyles.RoundtripKind);
        return tokenRequestedDate;
    }

    private async Task<TeslaToken> GetAccessToken()
    {
        logger.LogTrace("{method}()", nameof(GetAccessToken));
        var token = await teslaSolarChargerContext.TeslaTokens
            .OrderByDescending(t => t.ExpiresAtUtc)
            .FirstAsync().ConfigureAwait(false);
        if (token.UnauthorizedCounter > constants.MaxTokenUnauthorizedCount)
        {
            logger.LogError("Token unauthorized counter is too high. Request a new token.");
            throw new InvalidOperationException("Token unauthorized counter is too high. Request a new token.");
        }
        return token;
    }

    private async Task HandleNonSuccessTeslaApiStatusCodes(HttpStatusCode statusCode, TeslaToken token,
        string responseString, string? vin = null)
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
            car.RateLimitedUntil = nextAllowedUtcTime;
        }
        else
        {
            logger.LogWarning(
                "Staus Code {statusCode} is currently not handled, look into https://developer.tesla.com/docs/fleet-api#response-codes to check status code information. Response: {responseString}",
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
