using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class TeslaFleetApiService : ITeslaService
{
    private readonly ILogger<TeslaFleetApiService> _logger;
    private readonly ITeslaSolarChargerContext _teslaSolarChargerContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ITeslamateContext _teslamateContext;
    private readonly IConfigurationWrapper _configurationWrapper;

    private readonly string _chargeStartComand = "command/charge_start";
    private readonly string _chargeStopComand = "command/charge_stop";
    private readonly string _setChargingAmps = "command/set_charging_amps";
    private readonly string _wakeUpComand = "wake_up";

    public TeslaFleetApiService(ILogger<TeslaFleetApiService> logger, ITeslaSolarChargerContext teslaSolarChargerContext,
        IDateTimeProvider dateTimeProvider, ITeslamateContext teslamateContext, IConfigurationWrapper configurationWrapper)
    {
        _logger = logger;
        _teslaSolarChargerContext = teslaSolarChargerContext;
        _dateTimeProvider = dateTimeProvider;
        _teslamateContext = teslamateContext;
        _configurationWrapper = configurationWrapper;
    }

    public async Task StartCharging(int carId, int startAmp, CarStateEnum? carState)
    {
        _logger.LogTrace("{method}({carId}, {startAmp}, {carState})", nameof(StartCharging), carId, startAmp, carState);
        var id = await _teslamateContext.Cars.Where(c => c.Id == carId).Select(c => c.Eid).FirstAsync().ConfigureAwait(false);
        var result = await SendCommandToTeslaApi(id, _chargeStartComand).ConfigureAwait(false);
    }


    public async Task WakeUpCar(int carId)
    {
        _logger.LogTrace("{method}({carId})", nameof(WakeUpCar), carId);
        var id = await _teslamateContext.Cars.Where(c => c.Id == carId).Select(c => c.Eid).FirstAsync().ConfigureAwait(false);
        var result = await SendCommandToTeslaApi(id, _wakeUpComand).ConfigureAwait(false);
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

    private async Task<DtoVehicleCommandResult?> SendCommandToTeslaApi(long id, string commandName, string contentData = "{}")
    {
        var accessToken = await GetAccessTokenAsync().ConfigureAwait(false);
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var content = new StringContent(contentData, System.Text.Encoding.UTF8, "application/json");
        var requestUri = $"https://fleet-api.prd.eu.vn.cloud.tesla.com/api/1/vehicles/{id}/{commandName}";
        var response = await httpClient.PostAsync(requestUri, content).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<DtoVehicleCommandResult>().ConfigureAwait(false);
    }

    private async Task<string> GetAccessTokenAsync()
    {
        _logger.LogTrace("{method}()", nameof(GetAccessTokenAsync));
        var token = await _teslaSolarChargerContext.TeslaTokens
            .OrderByDescending(t => t.ExpiresAtUtc)
            .FirstAsync().ConfigureAwait(false);
        var minimumTokenLifeTime = TimeSpan.FromMinutes(5);
        if (token.ExpiresAtUtc < (_dateTimeProvider.UtcNow() + minimumTokenLifeTime))
        {
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
            var newToken = JsonConvert.DeserializeObject<DtoTeslaFleetApiToken>(responseString) ?? throw new InvalidDataException("Could not get token from string.");
            token.AccessToken = newToken.AccessToken;
            token.RefreshToken = newToken.RefreshToken;
            token.IdToken = newToken.IdToken;
            token.ExpiresAtUtc = _dateTimeProvider.UtcNow().AddSeconds(newToken.ExpiresIn);
            await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
        }
        return token.AccessToken;
    }

    //Just to look up code, not used.
    //private async Task GetAllAccountVehicles(DtoTeslaToken token)
    //{
    //    using var httpClient = new HttpClient();
    //    var request = new HttpRequestMessage(HttpMethod.Get, $"https://fleet-api.prd.eu.vn.cloud.tesla.com/api/1/vehicles");
    //    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
    //    var vehicleResponse = await httpClient.SendAsync(request).ConfigureAwait(false);
    //    var responseString = await vehicleResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
    //}
}
