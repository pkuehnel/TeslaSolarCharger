using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Globalization;
using System.Net.Http.Headers;
using TeslaSolarCharger.Model.Contracts;
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
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedBackend.Contracts;

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

    private readonly string _chargeStartComand = "command/charge_start";
    private readonly string _chargeStopComand = "command/charge_stop";
    private readonly string _setChargingAmps = "command/set_charging_amps";
    private readonly string _wakeUpComand = "wake_up";

    public TeslaFleetApiService(ILogger<TeslaFleetApiService> logger, ITeslaSolarChargerContext teslaSolarChargerContext,
        IDateTimeProvider dateTimeProvider, ITeslamateContext teslamateContext, IConfigurationWrapper configurationWrapper,
        ITeslamateApiService teslamateApiService, IConstants constants, ITscConfigurationService tscConfigurationService)
    {
        _logger = logger;
        _teslaSolarChargerContext = teslaSolarChargerContext;
        _dateTimeProvider = dateTimeProvider;
        _teslamateContext = teslamateContext;
        _configurationWrapper = configurationWrapper;
        _teslamateApiService = teslamateApiService;
        _constants = constants;
        _tscConfigurationService = tscConfigurationService;
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

    public Task SetScheduledCharging(int carId, DateTimeOffset? chargingStartTime)
    {
        _logger.LogError("This is currently not supported with Fleet API");
        return Task.CompletedTask;
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
        _logger.LogDebug("Response: {responseString}", responseString);
        return await response.Content.ReadFromJsonAsync<DtoVehicleCommandResult>().ConfigureAwait(false);
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
            case FleetApiTokenState.NotReceived:
                break;
            case FleetApiTokenState.Expired:
                break;
            case FleetApiTokenState.UpToDate:
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
            ExpiresAtUtc = _dateTimeProvider.UtcNow().AddMinutes(2),
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
        //ToDo: return not requested if request is older than x -> Currently not nown as not known how old a code can be to create a token out of it.
        return new DtoValue<FleetApiTokenState>(FleetApiTokenState.NotReceived);
    }

    private async Task<TeslaToken> GetAccessTokenAndRefreshWhenNeededAsync()
    {
        _logger.LogTrace("{method}()", nameof(GetAccessTokenAndRefreshWhenNeededAsync));
        var token = await _teslaSolarChargerContext.TeslaTokens
            .OrderByDescending(t => t.ExpiresAtUtc)
            .FirstAsync().ConfigureAwait(false);
        var minimumTokenLifeTime = TimeSpan.FromMinutes(5);
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
