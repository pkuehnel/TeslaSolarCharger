using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Dtos;
using TeslaSolarCharger.Server.MappingExtensions;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class TeslaFleetApiService : ITeslaService
{
    private readonly ILogger<TeslaFleetApiService> _logger;
    private readonly ITeslaSolarChargerContext _teslaSolarChargerContext;
    private readonly IMapperConfigurationFactory _mapperConfigurationFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ITeslamateContext _teslamateContext;

    private readonly string _chargeStartComand = "command/charge_start";
    private readonly string _chargeStopComand = "command/charge_stop";
    private readonly string _setChargingAmps = "command/set_charging_amps";
    private readonly string _wakeUpComand = "wake_up";

    public TeslaFleetApiService(ILogger<TeslaFleetApiService> logger, ITeslaSolarChargerContext teslaSolarChargerContext,
        IMapperConfigurationFactory mapperConfigurationFactory, IDateTimeProvider dateTimeProvider, ITeslamateContext teslamateContext)
    {
        _logger = logger;
        _teslaSolarChargerContext = teslaSolarChargerContext;
        _mapperConfigurationFactory = mapperConfigurationFactory;
        _dateTimeProvider = dateTimeProvider;
        _teslamateContext = teslamateContext;
    }

    public async Task StartCharging(int carId, int startAmp, CarStateEnum? carState)
    {
        _logger.LogTrace("{method}({carId}, {startAmp}, {carState})", nameof(StartCharging), carId, startAmp, carState);
        var token = await GetTeslaTokenAsync().ConfigureAwait(false);
        var id = await _teslamateContext.Cars.Where(c => c.Id == carId).Select(c => c.Eid).FirstAsync().ConfigureAwait(false);
        var result = await SendCommandToTeslaApi(token, id, _chargeStartComand).ConfigureAwait(false);
    }


    public async Task WakeUpCar(int carId)
    {
        _logger.LogTrace("{method}({carId})", nameof(WakeUpCar), carId);
        var token = await GetTeslaTokenAsync().ConfigureAwait(false);
        var id = await _teslamateContext.Cars.Where(c => c.Id == carId).Select(c => c.Eid).FirstAsync().ConfigureAwait(false);
        var result = await SendCommandToTeslaApi(token, id, _wakeUpComand).ConfigureAwait(false);
    }

    public async Task StopCharging(int carId)
    {
        _logger.LogTrace("{method}({carId})", nameof(StopCharging), carId);
        var token = await GetTeslaTokenAsync().ConfigureAwait(false);
        var id = await _teslamateContext.Cars.Where(c => c.Id == carId).Select(c => c.Eid).FirstAsync().ConfigureAwait(false);
        var result = await SendCommandToTeslaApi(token, id, _chargeStopComand).ConfigureAwait(false);
    }

    public async Task SetAmp(int carId, int amps)
    {
        _logger.LogTrace("{method}({carId}, {amps})", nameof(SetAmp), carId, amps);
        var token = await GetTeslaTokenAsync().ConfigureAwait(false);
        var id = await _teslamateContext.Cars.Where(c => c.Id == carId).Select(c => c.Eid).FirstAsync().ConfigureAwait(false);
        var commandData = $"{{\"charging_amps\":{amps}}}";
        var result = await SendCommandToTeslaApi(token, id, _setChargingAmps, commandData).ConfigureAwait(false);
    }

    public Task SetScheduledCharging(int carId, DateTimeOffset? chargingStartTime)
    {
        _logger.LogError("This is currently not supported with Fleet API");
        return Task.CompletedTask;
    }

    private static async Task<DtoVehicleCommandResult?> SendCommandToTeslaApi(DtoTeslaToken token, long id, string commandName, string contentData = "{}")
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        var content = new StringContent(contentData, System.Text.Encoding.UTF8, "application/json");
        var requestUri = $"https://fleet-api.prd.eu.vn.cloud.tesla.com/api/1/vehicles/{id}/{commandName}";
        var response = await httpClient.PostAsync(requestUri, content).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<DtoVehicleCommandResult>().ConfigureAwait(false);
    }

    private async Task<DtoTeslaToken> GetTeslaTokenAsync()
    {
        var mapper = _mapperConfigurationFactory.Create(cfg =>
        {
            cfg.CreateMap<TeslaToken, DtoTeslaToken>()
                ;
        });
        var currentDateTime = _dateTimeProvider.UtcNow();
        var token = await _teslaSolarChargerContext.TeslaTokens
            .Where(t => t.ExpiresAtUtc > currentDateTime)
            .OrderByDescending(t => t.ExpiresAtUtc)
            .ProjectTo<DtoTeslaToken>(mapper)
            .FirstAsync().ConfigureAwait(false);
        return token;
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
