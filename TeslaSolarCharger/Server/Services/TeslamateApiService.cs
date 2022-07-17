﻿using System.Text;
using Newtonsoft.Json;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class TeslamateApiService : ITeslaService
{
    private readonly ILogger<TeslamateApiService> _logger;
    private readonly ITelegramService _telegramService;
    private readonly ISettings _settings;
    private readonly string _teslaMateBaseUrl;

    public TeslamateApiService(ILogger<TeslamateApiService> logger, ITelegramService telegramService, 
        ISettings settings, IConfigurationWrapper configurationWrapper)
    {
        _logger = logger;
        _telegramService = telegramService;
        _settings = settings;
        _teslaMateBaseUrl = configurationWrapper.TeslaMateApiBaseUrl();
    }

    public async Task StartCharging(int carId, int startAmp, CarStateEnum? carState)
    {
        _logger.LogTrace("{method}({param1}, {param2}, {param3})", nameof(StartCharging), carId, startAmp, carState);

        if (carState == CarStateEnum.Offline ||
            carState == CarStateEnum.Asleep)
        {
            _logger.LogInformation("Wakeup car before charging");
            await WakeUpCar(carId);
        }

        if (carState == CarStateEnum.Suspended)
        {
            _logger.LogInformation("Logging is suspended");
            await ResumeLogging(carId);
        }

        var url = $"{_teslaMateBaseUrl}/api/v1/cars/{carId}/command/charge_start";

        var result = await SendPostToTeslaMate(url).ConfigureAwait(false);

        await SetAmp(carId, startAmp).ConfigureAwait(false);

        _logger.LogTrace("result: {resultContent}", result.Content.ReadAsStringAsync().Result);
    }

    public async Task StopCharging(int carId)
    {
        _logger.LogTrace("{method}({param1})", nameof(StopCharging), carId);
        var url = $"{_teslaMateBaseUrl}/api/v1/cars/{carId}/command/charge_stop";

        var result = await SendPostToTeslaMate(url).ConfigureAwait(false);

        var car = _settings.Cars.First(c => c.Id == carId);
        car.CarState.LastSetAmp = 0;

        _logger.LogTrace("result: {resultContent}", result.Content.ReadAsStringAsync().Result);
    }

    public async Task WakeUpCar(int carId)
    {
        _logger.LogTrace("{method}({param})", nameof(WakeUpCar), carId);

        var url = $"{_teslaMateBaseUrl}/api/v1/cars/{carId}/wake_up";

        var result = await SendPostToTeslaMate(url).ConfigureAwait(false);
        _logger.LogTrace("result: {resultContent}", result.Content.ReadAsStringAsync().Result);

        await ResumeLogging(carId);

        await Task.Delay(TimeSpan.FromSeconds(20));
    }

    public async Task SetAmp(int carId, int amps)
    {
        _logger.LogTrace("{method}({param1}, {param2})", nameof(SetAmp), carId, amps);
        var car = _settings.Cars.First(c => c.Id == carId);
        var parameters = new Dictionary<string, string>()
        {
            {"charging_amps", amps.ToString()},
        };

        var url = $"{_teslaMateBaseUrl}/api/v1/cars/{carId}/command/set_charging_amps";

        HttpResponseMessage? result = null;
        if (car.CarState.ChargerRequestedCurrent != amps)
        {
            result = await SendPostToTeslaMate(url, parameters).ConfigureAwait(false);
        }
        

        if (amps < 5 && car.CarState.LastSetAmp > 5
            || amps > 5 && car.CarState.LastSetAmp < 5)
        {
            await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            result = await SendPostToTeslaMate(url, parameters).ConfigureAwait(false);
        }

        car.CarState.LastSetAmp = amps;

        if (result != null)
        {
            _logger.LogTrace("result: {resultContent}", result.Content.ReadAsStringAsync().Result);
        }
    }

    private async Task ResumeLogging(int carId)
    {
        _logger.LogTrace("{method}({param1})", nameof(ResumeLogging), carId);
        var url = $"{_teslaMateBaseUrl}/api/v1/cars/{carId}/logging/resume";
        using var httpClient = new HttpClient();
        var response = await httpClient.PutAsync(url, null);
        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpResponseMessage> SendPostToTeslaMate(string url, Dictionary<string, string>? parameters = null)
    {
        _logger.LogTrace("{method}({param1}, {param2})", nameof(SendPostToTeslaMate), url, parameters);
        var jsonString = JsonConvert.SerializeObject(parameters);
        var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
        using var httpClient = new HttpClient();
        var response = await httpClient.PostAsync(url, content).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var responseContentString = await response.Content.ReadAsStringAsync();
            _logger.LogError("Error while sending post to TeslaMate. Response: {response}", responseContentString);
            await _telegramService.SendMessage($"Error while sending post to TeslaMate.\r\n RequestBody: {jsonString} \r\n Response: {responseContentString}");
        }
        response.EnsureSuccessStatusCode();
        return response;
    }
}