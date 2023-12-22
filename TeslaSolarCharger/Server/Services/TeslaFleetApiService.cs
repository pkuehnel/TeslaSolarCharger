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
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedBackend.Contracts;
using TeslaSolarCharger.SharedBackend.Dtos;
using static System.Net.WebRequestMethods;
using Car = TeslaSolarCharger.Shared.Dtos.Settings.Car;

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
    ISettings settings)
    : ITeslaService, ITeslaFleetApiService
{
    private DtoFleetApiRequest ChargeStartRequest => new()
    {
        RequestUrl = "command/charge_start",
        NeedsProxy = true,
    };
    private DtoFleetApiRequest ChargeStopRequest => new()
    {
        RequestUrl = "command/charge_stop",
        NeedsProxy = true,
    };
    private DtoFleetApiRequest SetChargingAmpsRequest => new()
    {
        RequestUrl = "command/set_charging_amps",
        NeedsProxy = true,
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

    public async Task StartCharging(int carId, int startAmp, CarStateEnum? carState)
    {
        logger.LogTrace("{method}({carId}, {startAmp}, {carState})", nameof(StartCharging), carId, startAmp, carState);
        if (startAmp == 0)
        {
            logger.LogDebug("Should start charging with 0 amp. Skipping charge start.");
            return;
        }
        await WakeUpCarIfNeeded(carId, carState).ConfigureAwait(false);

        var vin = await GetVinByCarId(carId).ConfigureAwait(false);
        await SetAmp(carId, startAmp).ConfigureAwait(false);

        var result = await SendCommandToTeslaApi<DtoVehicleCommandResult>(vin, ChargeStartRequest).ConfigureAwait(false);
    }


    public async Task WakeUpCar(int carId)
    {
        logger.LogTrace("{method}({carId})", nameof(WakeUpCar), carId);
        var vin = await GetVinByCarId(carId).ConfigureAwait(false);
        var result = await SendCommandToTeslaApi<DtoVehicleWakeUpResult>(vin, WakeUpRequest).ConfigureAwait(false);
        await teslamateApiService.ResumeLogging(carId).ConfigureAwait(false);

        await Task.Delay(TimeSpan.FromSeconds(20)).ConfigureAwait(true);
    }

    public async Task StopCharging(int carId)
    {
        logger.LogTrace("{method}({carId})", nameof(StopCharging), carId);
        var vin = await GetVinByCarId(carId).ConfigureAwait(false);
        var result = await SendCommandToTeslaApi<DtoVehicleCommandResult>(vin, ChargeStopRequest).ConfigureAwait(false);
    }

    public async Task SetAmp(int carId, int amps)
    {
        logger.LogTrace("{method}({carId}, {amps})", nameof(SetAmp), carId, amps);
        var vin = await GetVinByCarId(carId).ConfigureAwait(false);
        var commandData = $"{{\"charging_amps\":{amps}}}";
        var result = await SendCommandToTeslaApi<DtoVehicleCommandResult>(vin, SetChargingAmpsRequest, commandData).ConfigureAwait(false);
    }

    public async Task SetScheduledCharging(int carId, DateTimeOffset? chargingStartTime)
    {
        logger.LogTrace("{method}({param1}, {param2})", nameof(SetScheduledCharging), carId, chargingStartTime);
        var vin = await GetVinByCarId(carId).ConfigureAwait(false);
        var car = settings.Cars.First(c => c.Id == carId);
        if (!IsChargingScheduleChangeNeeded(chargingStartTime, dateTimeProvider.DateTimeOffSetNow(), car, out var parameters))
        {
            logger.LogDebug("No change in updating scheduled charging needed.");
            return;
        }

        await WakeUpCarIfNeeded(carId, car.CarState.State).ConfigureAwait(false);

        var result = await SendCommandToTeslaApi<DtoVehicleCommandResult>(vin, SetScheduledChargingRequest, JsonConvert.SerializeObject(parameters)).ConfigureAwait(false);
        //assume update was sucessfull as update is not working after mosquitto restart (or wrong cached State)
        if (parameters["enable"] == "false")
        {
            car.CarState.ScheduledChargingStartTime = null;
        }
    }

    public async Task SetChargeLimit(int carId, int limitSoC)
    {
        logger.LogTrace("{method}({param1}, {param2})", nameof(SetChargeLimit), carId, limitSoC);
        var vin = await GetVinByCarId(carId).ConfigureAwait(false);
        var car = settings.Cars.First(c => c.Id == carId);
        await WakeUpCarIfNeeded(carId, car.CarState.State).ConfigureAwait(false);
        var parameters = new Dictionary<string, int>()
        {
            { "percent", limitSoC },
        };
        await SendCommandToTeslaApi<DtoVehicleCommandResult>(vin, SetChargeLimitRequest, JsonConvert.SerializeObject(parameters)).ConfigureAwait(false);
    }

    public async Task<DtoValue<bool>> TestFleetApiAccess(int carId)
    {
        logger.LogTrace("{method}({carId})", nameof(TestFleetApiAccess), carId);
        var vin = await GetVinByCarId(carId).ConfigureAwait(false);
        var car = settings.Cars.First(c => c.Id == carId);
        try
        {
            await WakeUpCarIfNeeded(carId, car.CarState.State).ConfigureAwait(false);
            var result = await SendCommandToTeslaApi<DtoVehicleCommandResult>(vin, OpenChargePortDoorRequest).ConfigureAwait(false);
            var successResult = result?.Response?.Result == true;
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

    public async Task OpenChargePortDoor(int carId)
    {
        logger.LogTrace("{method}({carId})", nameof(OpenChargePortDoor), carId);
        var vin = await GetVinByCarId(carId).ConfigureAwait(false);
        var result = await SendCommandToTeslaApi<DtoVehicleCommandResult>(vin, OpenChargePortDoorRequest).ConfigureAwait(false);
    }

    private async Task<string> GetVinByCarId(int carId)
    {
        var vin = await teslamateContext.Cars.Where(c => c.Id == carId).Select(c => c.Vin).FirstAsync().ConfigureAwait(false);
        if (string.IsNullOrEmpty(vin))
        {
            logger.LogError("Could not get VIN for car ID {carId}", carId);
            throw new InvalidOperationException("Could not find VIN");
        }

        return vin;
    }

    internal bool IsChargingScheduleChangeNeeded(DateTimeOffset? chargingStartTime, DateTimeOffset currentDate, Car car, out Dictionary<string, string> parameters)
    {
        logger.LogTrace("{method}({startTime}, {currentDate}, {carId}, {parameters})", nameof(IsChargingScheduleChangeNeeded), chargingStartTime, currentDate, car.Id, nameof(parameters));
        parameters = new Dictionary<string, string>();
        if (chargingStartTime != null)
        {
            logger.LogTrace("{chargingStartTime} is not null", nameof(chargingStartTime));
            chargingStartTime = RoundToNextQuarterHour(chargingStartTime.Value);
        }
        if (car.CarState.ScheduledChargingStartTime == chargingStartTime)
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

        if (car.CarState.ScheduledChargingStartTime == chargingStartTime)
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

        if (car.CarState.ScheduledChargingStartTime == null && !scheduledChargeShouldBeSet)
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

    private async Task<DtoGenericTeslaResponse<T>?> SendCommandToTeslaApi<T>(string vin, DtoFleetApiRequest fleetApiRequest, string contentData = "{}") where T : class
    {
        logger.LogTrace("{method}({vin}, {@fleetApiRequest}, {contentData})", nameof(SendCommandToTeslaApi), vin, fleetApiRequest, contentData);
        var accessToken = await GetAccessTokenAndRefreshWhenNeededAsync().ConfigureAwait(false);
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.AccessToken);
        var content = new StringContent(contentData, System.Text.Encoding.UTF8, "application/json");
        
        var baseUrl = GetFleetApiBaseUrl(accessToken.Region, fleetApiRequest.NeedsProxy);
        var requestUri = $"{baseUrl}api/1/vehicles/{vin}/{fleetApiRequest.RequestUrl}";
        settings.TeslaApiRequestCounter++;
        var response = await httpClient.PostAsync(requestUri, content).ConfigureAwait(false);
        var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var teslaCommandResultResponse = JsonConvert.DeserializeObject<DtoGenericTeslaResponse<T>>(responseString);
        if (!response.IsSuccessStatusCode)
        {
            await backendApiService.PostErrorInformation(nameof(TeslaFleetApiService), nameof(SendCommandToTeslaApi),
                $"Sending command to Tesla API resulted in non succes status code: {response.StatusCode} : Command name:{fleetApiRequest.RequestUrl}, Content data:{contentData}. Response string: {responseString}").ConfigureAwait(false);
            await HandleNonSuccessTeslaApiStatusCodes(response.StatusCode, accessToken, responseString, vin).ConfigureAwait(false);
        }

        if (response.IsSuccessStatusCode && (teslaCommandResultResponse?.Response is DtoVehicleCommandResult vehicleCommandResult))
        {
            if (vehicleCommandResult.Result != true)
            {
                await backendApiService.PostErrorInformation(nameof(TeslaFleetApiService), nameof(SendCommandToTeslaApi),
                        $"Result of command request is false {fleetApiRequest.RequestUrl}, {contentData}. Response string: {responseString}")
                    .ConfigureAwait(false);
            }
        }
        logger.LogDebug("Response: {responseString}", responseString);
        return teslaCommandResultResponse;
    }

    private string GetFleetApiBaseUrl(TeslaFleetApiRegion region, bool useProxyBaseUrl)
    {
        if (useProxyBaseUrl && configurationWrapper.UseFleetApiProxy())
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

    public async Task RefreshTokenAsync()
    {
        logger.LogTrace("{method}()", nameof(RefreshTokenAsync));
        var tokenState = (await GetFleetApiTokenState().ConfigureAwait(false)).Value;
        switch (tokenState)
        {
            case FleetApiTokenState.NotNeeded:
                logger.LogDebug("Refreshing token not needed.");
                return;
            case FleetApiTokenState.NotRequested:
                logger.LogDebug("No token has been requested, yet.");
                return;
            case FleetApiTokenState.TokenRequestExpired:
                logger.LogError("Your token request has expired, create a new one.");
                return;
            case FleetApiTokenState.TokenUnauthorized:
                logger.LogError("Your refresh token is unauthorized, create a new token.");
                return;
            case FleetApiTokenState.NotReceived:
                break;
            case FleetApiTokenState.Expired:
                break;
            case FleetApiTokenState.UpToDate:
                logger.LogDebug("Token is up to date.");
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        var token = await teslaSolarChargerContext.TeslaTokens.FirstOrDefaultAsync().ConfigureAwait(false);
        if (token == null)
        {
            using var httpClient = new HttpClient();
            var installationId = await tscConfigurationService.GetInstallationId().ConfigureAwait(false);
            var url = configurationWrapper.BackendApiBaseUrl() + $"Tsc/DeliverAuthToken?installationId={installationId}";
            var response = await httpClient.GetAsync(url).ConfigureAwait(false);
            var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                await backendApiService.PostErrorInformation(nameof(TeslaFleetApiService), nameof(SendCommandToTeslaApi),
                    $"Getting token from TscBackend. Response status code: {response.StatusCode} Response string: {responseString}").ConfigureAwait(false);
            }
            response.EnsureSuccessStatusCode();
            var newToken = JsonConvert.DeserializeObject<DtoTeslaTscDeliveryToken>(responseString) ?? throw new InvalidDataException("Could not get token from string.");
            await AddNewTokenAsync(newToken).ConfigureAwait(false);
        }
        var dbToken = await GetAccessTokenAndRefreshWhenNeededAsync().ConfigureAwait(false);
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
        });
        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<DtoValue<FleetApiTokenState>> GetFleetApiTokenState()
    {
        if (!configurationWrapper.UseFleetApi())
        {
            return new DtoValue<FleetApiTokenState>(FleetApiTokenState.NotNeeded);
        }
        var isCurrentRefreshTokenUnauthorized = await teslaSolarChargerContext.TscConfigurations
            .Where(c => c.Key == constants.TokenRefreshUnauthorized)
            .AnyAsync().ConfigureAwait(false);
        if (isCurrentRefreshTokenUnauthorized)
        {
            return new DtoValue<FleetApiTokenState>(FleetApiTokenState.TokenUnauthorized);
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
            return new DtoValue<FleetApiTokenState>(token.ExpiresAtUtc < dateTimeProvider.UtcNow() ? FleetApiTokenState.Expired : FleetApiTokenState.UpToDate);
        }
        var tokenRequestedDateString = await teslaSolarChargerContext.TscConfigurations
            .Where(c => c.Key == constants.FleetApiTokenRequested)
            .Select(c => c.Value)
            .FirstOrDefaultAsync().ConfigureAwait(false);
        if (tokenRequestedDateString == null)
        {
            return new DtoValue<FleetApiTokenState>(FleetApiTokenState.NotRequested);
        }
        var tokenRequestedDate = DateTime.Parse(tokenRequestedDateString, null, DateTimeStyles.RoundtripKind);
        var currentDate = dateTimeProvider.UtcNow();
        if (tokenRequestedDate < currentDate.AddMinutes(-5))
        {
            return new DtoValue<FleetApiTokenState>(FleetApiTokenState.TokenRequestExpired);
        }
        return new DtoValue<FleetApiTokenState>(FleetApiTokenState.NotReceived);
    }

    private async Task<TeslaToken> GetAccessTokenAndRefreshWhenNeededAsync()
    {
        logger.LogTrace("{method}()", nameof(GetAccessTokenAndRefreshWhenNeededAsync));
        var token = await teslaSolarChargerContext.TeslaTokens
            .OrderByDescending(t => t.ExpiresAtUtc)
            .FirstAsync().ConfigureAwait(false);
        var minimumTokenLifeTime = TimeSpan.FromMinutes(5);
        var isCurrentRefreshTokenUnauthorized = await teslaSolarChargerContext.TscConfigurations
            .Where(c => c.Key == constants.TokenRefreshUnauthorized)
            .AnyAsync().ConfigureAwait(false);
        if (isCurrentRefreshTokenUnauthorized)
        {
            logger.LogError("Token is unauthorized");
            throw new InvalidDataException("Current Tesla Fleet Api Token is unauthorized");
        }
        if (token.ExpiresAtUtc < (dateTimeProvider.UtcNow() + minimumTokenLifeTime))
        {
            logger.LogInformation("Token is expired. Getting new token.");
            using var httpClient = new HttpClient();
            var tokenUrl = "https://auth.tesla.com/oauth2/v3/token";
            var requestData = new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "client_id", configurationWrapper.FleetApiClientId() },
                { "refresh_token", token.RefreshToken },
            };
            var encodedContent = new FormUrlEncodedContent(requestData);
            encodedContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
            var response = await httpClient.PostAsync(tokenUrl, encodedContent).ConfigureAwait(false);
            var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                await backendApiService.PostErrorInformation(nameof(TeslaFleetApiService), nameof(SendCommandToTeslaApi),
                    $"Refreshing token did result in non success status code. Response status code: {response.StatusCode} Response string: {responseString}").ConfigureAwait(false);
                await HandleNonSuccessTeslaApiStatusCodes(response.StatusCode, token, responseString).ConfigureAwait(false);
            }
            response.EnsureSuccessStatusCode();
            var newToken = JsonConvert.DeserializeObject<DtoTeslaFleetApiRefreshToken>(responseString) ?? throw new InvalidDataException("Could not get token from string.");
            token.AccessToken = newToken.AccessToken;
            token.RefreshToken = newToken.RefreshToken;
            token.IdToken = newToken.IdToken;
            token.ExpiresAtUtc = dateTimeProvider.UtcNow().AddSeconds(newToken.ExpiresIn);
            await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
            logger.LogInformation("New Token saved to database.");
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
                "Your token or refresh token is invalid. Very likely you have changed your Tesla password.");
            teslaSolarChargerContext.TeslaTokens.Remove(token);
            teslaSolarChargerContext.TscConfigurations.Add(new TscConfiguration()
            {
                Key = constants.TokenRefreshUnauthorized, Value = responseString,
            });
        }
        else if (statusCode == HttpStatusCode.Forbidden)
        {
            logger.LogError("You did not select all scopes, so TSC can't send commands to your car.");
            teslaSolarChargerContext.TeslaTokens.Remove(token);
            teslaSolarChargerContext.TscConfigurations.Add(new TscConfiguration()
            {
                Key = constants.TokenMissingScopes, Value = responseString,
            });
        }
        else if (statusCode == HttpStatusCode.InternalServerError
                 && responseString.Contains("vehicle rejected request: your public key has not been paired with the vehicle"))
        {
            logger.LogError("Vehicle {vin} is not paired with TSC. Add The public key to the vehicle", vin);
            var notPairedKey = string.Format(constants.VehicleNotPaired, vin);
            var tscConfig = await teslaSolarChargerContext.TscConfigurations
                .FirstOrDefaultAsync(c => c.Key == notPairedKey).ConfigureAwait(false);
            if (tscConfig == default)
            {
                tscConfig = new TscConfiguration() { Key = notPairedKey, };
                teslaSolarChargerContext.TscConfigurations.Add(tscConfig);
            }
            tscConfig.Value = dateTimeProvider.UtcNow().ToString("o");
        }
        else
        {
            logger.LogWarning(
                "Staus Code {statusCode} is currently not handled, look into https://developer.tesla.com/docs/fleet-api#response-codes to check status code information",
                statusCode);
            return;
        }

        await teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }
}
