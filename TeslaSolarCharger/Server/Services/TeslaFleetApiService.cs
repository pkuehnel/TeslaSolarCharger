using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Globalization;
using System.Net.Http.Headers;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaMate;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.Dtos.TeslaFleetApi;
using TeslaSolarCharger.Server.Dtos.TscBackend;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedBackend.Contracts;
using Car = TeslaSolarCharger.Shared.Dtos.Settings.Car;

namespace TeslaSolarCharger.Server.Services;

public class TeslaFleetApiService : ITeslaService, ITeslaFleetApiService
{
    private readonly ILogger<TeslaFleetApiService> _logger;
    private readonly ITeslaSolarChargerContext _teslaSolarChargerContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ITeslamateContext _teslamateContext;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly ITeslamateApiService _teslamateApiService;
    private readonly IConstants _constants;
    private readonly ITscConfigurationService _tscConfigurationService;
    private readonly IBackendApiService _backendApiService;
    private readonly ISettings _settings;

    private readonly string _chargeStartComand = "command/charge_start";
    private readonly string _chargeStopComand = "command/charge_stop";
    private readonly string _setChargingAmps = "command/set_charging_amps";
    private readonly string _setScheduledCharging = "command/set_scheduled_charging";
    private readonly string _setSocLimit = "command/set_charge_limit";
    private readonly string _wakeUpComand = "wake_up";

    public TeslaFleetApiService(ILogger<TeslaFleetApiService> logger, ITeslaSolarChargerContext teslaSolarChargerContext,
        IDateTimeProvider dateTimeProvider, ITeslamateContext teslamateContext, IConfigurationWrapper configurationWrapper,
        ITeslamateApiService teslamateApiService, IConstants constants, ITscConfigurationService tscConfigurationService,
        IBackendApiService backendApiService, ISettings settings)
    {
        _logger = logger;
        _teslaSolarChargerContext = teslaSolarChargerContext;
        _dateTimeProvider = dateTimeProvider;
        _teslamateContext = teslamateContext;
        _configurationWrapper = configurationWrapper;
        _teslamateApiService = teslamateApiService;
        _constants = constants;
        _tscConfigurationService = tscConfigurationService;
        _backendApiService = backendApiService;
        _settings = settings;
    }

    public async Task StartCharging(int carId, int startAmp, CarStateEnum? carState)
    {
        _logger.LogTrace("{method}({carId}, {startAmp}, {carState})", nameof(StartCharging), carId, startAmp, carState);
        if (startAmp == 0)
        {
            _logger.LogDebug("Should start charging with 0 amp. Skipping charge start.");
            return;
        }
        await WakeUpCarIfNeeded(carId, carState).ConfigureAwait(false);

        var id = await _teslamateContext.Cars.Where(c => c.Id == carId).Select(c => c.Eid).FirstAsync().ConfigureAwait(false);
        var result = await SendCommandToTeslaApi(id, _chargeStartComand).ConfigureAwait(false);
    }


    public async Task WakeUpCar(int carId)
    {
        _logger.LogTrace("{method}({carId})", nameof(WakeUpCar), carId);
        var id = await _teslamateContext.Cars.Where(c => c.Id == carId).Select(c => c.Eid).FirstAsync().ConfigureAwait(false);
        var result = await SendCommandToTeslaApi(id, _wakeUpComand).ConfigureAwait(false);
        await _teslamateApiService.ResumeLogging(carId).ConfigureAwait(false);

        await Task.Delay(TimeSpan.FromSeconds(20)).ConfigureAwait(false);
    }

    public async Task StopCharging(int carId)
    {
        _logger.LogTrace("{method}({carId})", nameof(StopCharging), carId);
        var id = await _teslamateContext.Cars.Where(c => c.Id == carId).Select(c => c.Eid).FirstAsync().ConfigureAwait(false);
        var result = await SendCommandToTeslaApi(id, _chargeStopComand).ConfigureAwait(false);
    }

    public async Task SetAmp(int carId, int amps)
    {
        _logger.LogTrace("{method}({carId}, {amps})", nameof(SetAmp), carId, amps);
        var id = await _teslamateContext.Cars.Where(c => c.Id == carId).Select(c => c.Eid).FirstAsync().ConfigureAwait(false);
        var commandData = $"{{\"charging_amps\":{amps}}}";
        var result = await SendCommandToTeslaApi(id, _setChargingAmps, commandData).ConfigureAwait(false);
    }

    public async Task SetScheduledCharging(int carId, DateTimeOffset? chargingStartTime)
    {
        _logger.LogTrace("{method}({param1}, {param2})", nameof(SetScheduledCharging), carId, chargingStartTime);
        var id = await _teslamateContext.Cars.Where(c => c.Id == carId).Select(c => c.Eid).FirstAsync().ConfigureAwait(false);
        var car = _settings.Cars.First(c => c.Id == carId);
        if (!IsChargingScheduleChangeNeeded(chargingStartTime, _dateTimeProvider.DateTimeOffSetNow(), car, out var parameters))
        {
            _logger.LogDebug("No change in updating scheduled charging needed.");
            return;
        }

        await WakeUpCarIfNeeded(carId, car.CarState.State).ConfigureAwait(false);

        var result = await SendCommandToTeslaApi(id, _setScheduledCharging, JsonConvert.SerializeObject(parameters)).ConfigureAwait(false);
        //assume update was sucessfull as update is not working after mosquitto restart (or wrong cached State)
        if (parameters["enable"] == "false")
        {
            car.CarState.ScheduledChargingStartTime = null;
        }
    }

    public async Task SetChargeLimit(int carId, int limitSoC)
    {
        _logger.LogTrace("{method}({param1}, {param2})", nameof(SetChargeLimit), carId, limitSoC);
        var id = await _teslamateContext.Cars.Where(c => c.Id == carId).Select(c => c.Eid).FirstAsync().ConfigureAwait(false);
        var car = _settings.Cars.First(c => c.Id == carId);
        await WakeUpCarIfNeeded(carId, car.CarState.State).ConfigureAwait(false);
        var parameters = new Dictionary<string, string>()
        {
            { "percent", limitSoC.ToString() },
        };
        await SendCommandToTeslaApi(id, _setSocLimit, JsonConvert.SerializeObject(parameters)).ConfigureAwait(false);
    }

    internal bool IsChargingScheduleChangeNeeded(DateTimeOffset? chargingStartTime, DateTimeOffset currentDate, Car car, out Dictionary<string, string> parameters)
    {
        _logger.LogTrace("{method}({startTime}, {currentDate}, {carId}, {parameters})", nameof(IsChargingScheduleChangeNeeded), chargingStartTime, currentDate, car.Id, nameof(parameters));
        parameters = new Dictionary<string, string>();
        if (chargingStartTime != null)
        {
            _logger.LogTrace("{chargingStartTime} is not null", nameof(chargingStartTime));
            chargingStartTime = RoundToNextQuarterHour(chargingStartTime.Value);
        }
        if (car.CarState.ScheduledChargingStartTime == chargingStartTime)
        {
            _logger.LogDebug("Correct charging start time already set.");
            return false;
        }

        if (chargingStartTime == null)
        {
            _logger.LogDebug("Set chargingStartTime to null.");
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
            _logger.LogDebug("Correct charging start time already set.");
            return true;
        }

        //ToDo: maybe disable scheduled charge in this case.
        if (timeUntilChargeStart <= TimeSpan.Zero || timeUntilChargeStart.TotalHours > 24)
        {
            _logger.LogDebug("Charge schedule should not be changed, as time until charge start is higher than 24 hours or lower than zero.");
            return false;
        }

        if (car.CarState.ScheduledChargingStartTime == null && !scheduledChargeShouldBeSet)
        {
            _logger.LogDebug("No charge schedule set and no charge schedule should be set.");
            return true;
        }
        _logger.LogDebug("Normal parameter set.");
        parameters = new Dictionary<string, string>()
        {
            { "enable", scheduledChargeShouldBeSet ? "true" : "false" },
            { "time", minutesFromMidNight.ToString() },
        };
        _logger.LogTrace("{@parameters}", parameters);
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
        _logger.LogDebug("Rounded charging Start time: {chargingStartTime}", chargingStartTime);
        return chargingStartTime;
    }

    private async Task WakeUpCarIfNeeded(int carId, CarStateEnum? carState)
    {
        switch (carState)
        {
            case CarStateEnum.Offline or CarStateEnum.Asleep:
                _logger.LogInformation("Wakeup car.");
                await WakeUpCar(carId).ConfigureAwait(false);
                break;
            case CarStateEnum.Suspended:
                _logger.LogInformation("Resume logging as is suspended");
                await _teslamateApiService.ResumeLogging(carId).ConfigureAwait(false);
                break;
        }
    }

    private async Task<DtoVehicleCommandResult?> SendCommandToTeslaApi(long id, string commandName, string contentData = "{}")
    {
        _logger.LogTrace("{method}({id}, {commandName}, {contentData})", nameof(SendCommandToTeslaApi), id, commandName, contentData);
        var accessToken = await GetAccessTokenAndRefreshWhenNeededAsync().ConfigureAwait(false);
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.AccessToken);
        var content = new StringContent(contentData, System.Text.Encoding.UTF8, "application/json");
        var regionCode = accessToken.Region switch
        {
            TeslaFleetApiRegion.Emea => "eu",
            TeslaFleetApiRegion.NorthAmerica => "na",
            _ => throw new NotImplementedException($"Region {accessToken.Region} is not implemented."),
        };
        var requestUri = $"https://fleet-api.prd.{regionCode}.vn.cloud.tesla.com/api/1/vehicles/{id}/{commandName}";
        var response = await httpClient.PostAsync(requestUri, content).ConfigureAwait(false);
        var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            await _backendApiService.PostErrorInformation(nameof(TeslaFleetApiService), nameof(SendCommandToTeslaApi),
                $"Sending command to Tesla API resulted in non succes status code: {response.StatusCode} : Command name:{commandName}, Content data:{contentData}. Response string: {responseString}").ConfigureAwait(false);
        }
        _logger.LogDebug("Response: {responseString}", responseString);
        var root = JsonConvert.DeserializeObject<DtoGenericTeslaResponse<DtoVehicleCommandResult>>(responseString);
        var result = root?.Response;
        if (result?.Result == false)
        {
            await _backendApiService.PostErrorInformation(nameof(TeslaFleetApiService), nameof(SendCommandToTeslaApi),
                $"Result of command request is false: {commandName}, {contentData}. Response string: {responseString}").ConfigureAwait(false);
        }

        return result ?? null;
    }

    public async Task RefreshTokenAsync()
    {
        _logger.LogTrace("{method}()", nameof(RefreshTokenAsync));
        var tokenState = (await GetFleetApiTokenState().ConfigureAwait(false)).Value;
        switch (tokenState)
        {
            case FleetApiTokenState.NotNeeded:
                _logger.LogDebug("Refreshing token not needed.");
                return;
            case FleetApiTokenState.NotRequested:
                _logger.LogDebug("No token has been requested, yet.");
                return;
            case FleetApiTokenState.TokenRequestExpired:
                _logger.LogError("Your token request has expired, create a new one.");
                return;
            case FleetApiTokenState.TokenUnauthorized:
                _logger.LogError("Your refresh token is unauthorized, create a new token.");
                return;
            case FleetApiTokenState.NotReceived:
                break;
            case FleetApiTokenState.Expired:
                break;
            case FleetApiTokenState.UpToDate:
                _logger.LogDebug("Token is up to date.");
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        var token = await _teslaSolarChargerContext.TeslaTokens.FirstOrDefaultAsync().ConfigureAwait(false);
        if (token == null)
        {
            using var httpClient = new HttpClient();
            var installationId = await _tscConfigurationService.GetInstallationId().ConfigureAwait(false);
            var url = _configurationWrapper.BackendApiBaseUrl() + $"Tsc/DeliverAuthToken?installationId={installationId}";
            var response = await httpClient.GetAsync(url).ConfigureAwait(false);
            var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                await _backendApiService.PostErrorInformation(nameof(TeslaFleetApiService), nameof(SendCommandToTeslaApi),
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
        var currentTokens = await _teslaSolarChargerContext.TeslaTokens.ToListAsync().ConfigureAwait(false);
        _teslaSolarChargerContext.TeslaTokens.RemoveRange(currentTokens);
        await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        _teslaSolarChargerContext.TeslaTokens.Add(new TeslaToken
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            IdToken = token.IdToken,
            ExpiresAtUtc = _dateTimeProvider.UtcNow().AddSeconds(token.ExpiresIn),
            Region = token.Region,
        });
        await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<DtoValue<FleetApiTokenState>> GetFleetApiTokenState()
    {
        if (!_configurationWrapper.UseFleetApi())
        {
            return new DtoValue<FleetApiTokenState>(FleetApiTokenState.NotNeeded);
        }
        var isCurrentRefreshTokenUnauthorized = await _teslaSolarChargerContext.TscConfigurations
            .Where(c => c.Key == _constants.TokenRefreshUnauthorized)
            .AnyAsync().ConfigureAwait(false);
        if (isCurrentRefreshTokenUnauthorized)
        {
            return new DtoValue<FleetApiTokenState>(FleetApiTokenState.TokenUnauthorized);
        }
        var token = await _teslaSolarChargerContext.TeslaTokens.FirstOrDefaultAsync().ConfigureAwait(false);
        if (token != null)
        {
            return new DtoValue<FleetApiTokenState>(token.ExpiresAtUtc < _dateTimeProvider.UtcNow() ? FleetApiTokenState.Expired : FleetApiTokenState.UpToDate);
        }
        var tokenRequestedDateString = await _teslaSolarChargerContext.TscConfigurations
            .Where(c => c.Key == _constants.FleetApiTokenRequested)
            .Select(c => c.Value)
            .FirstOrDefaultAsync().ConfigureAwait(false);
        if (tokenRequestedDateString == null)
        {
            return new DtoValue<FleetApiTokenState>(FleetApiTokenState.NotRequested);
        }
        var tokenRequestedDate = DateTime.Parse(tokenRequestedDateString, null, DateTimeStyles.RoundtripKind);
        var currentDate = _dateTimeProvider.UtcNow();
        if (tokenRequestedDate < currentDate.AddMinutes(-5))
        {
            return new DtoValue<FleetApiTokenState>(FleetApiTokenState.TokenRequestExpired);
        }
        return new DtoValue<FleetApiTokenState>(FleetApiTokenState.NotReceived);
    }

    private async Task<TeslaToken> GetAccessTokenAndRefreshWhenNeededAsync()
    {
        _logger.LogTrace("{method}()", nameof(GetAccessTokenAndRefreshWhenNeededAsync));
        var token = await _teslaSolarChargerContext.TeslaTokens
            .OrderByDescending(t => t.ExpiresAtUtc)
            .FirstAsync().ConfigureAwait(false);
        var minimumTokenLifeTime = TimeSpan.FromMinutes(5);
        var isCurrentRefreshTokenUnauthorized = await _teslaSolarChargerContext.TscConfigurations
            .Where(c => c.Key == _constants.TokenRefreshUnauthorized)
            .AnyAsync().ConfigureAwait(false);
        if (isCurrentRefreshTokenUnauthorized)
        {
            _logger.LogError("Token is unauthorized");
            throw new InvalidDataException("Current Tesla Fleet Api Token is unauthorized");
        }
        if (token.ExpiresAtUtc < (_dateTimeProvider.UtcNow() + minimumTokenLifeTime))
        {
            _logger.LogInformation("Token is expired. Getting new token.");
            using var httpClient = new HttpClient();
            var tokenUrl = "https://auth.tesla.com/oauth2/v3/token";
            var requestData = new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "client_id", _configurationWrapper.FleetApiClientId() },
                { "refresh_token", token.RefreshToken },
            };
            var encodedContent = new FormUrlEncodedContent(requestData);
            encodedContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
            var response = await httpClient.PostAsync(tokenUrl, encodedContent).ConfigureAwait(false);
            var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                await _backendApiService.PostErrorInformation(nameof(TeslaFleetApiService), nameof(SendCommandToTeslaApi),
                    $"Refreshing token did result in non success status code. Response status code: {response.StatusCode} Response string: {responseString}").ConfigureAwait(false);
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogError("Either you have changed your Tesla password or you did not select all scopes, so TSC can't send commands to your car.");
                    _teslaSolarChargerContext.TeslaTokens.Remove(token);
                    _teslaSolarChargerContext.TscConfigurations.Add(new TscConfiguration()
                    {
                        Key = _constants.TokenRefreshUnauthorized,
                        Value = responseString,
                    });
                    await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
                }
            }
            response.EnsureSuccessStatusCode();
            var newToken = JsonConvert.DeserializeObject<DtoTeslaFleetApiRefreshToken>(responseString) ?? throw new InvalidDataException("Could not get token from string.");
            token.AccessToken = newToken.AccessToken;
            token.RefreshToken = newToken.RefreshToken;
            token.IdToken = newToken.IdToken;
            token.ExpiresAtUtc = _dateTimeProvider.UtcNow().AddSeconds(newToken.ExpiresIn);
            await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
            _logger.LogInformation("New Token saved to database.");
        }
        return token;
    }

}
