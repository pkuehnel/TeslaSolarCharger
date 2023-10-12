using System.Text;
using Newtonsoft.Json;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class TeslamateApiService : ITeslaService
{
    private readonly ILogger<TeslamateApiService> _logger;
    private readonly ITelegramService _telegramService;
    private readonly ISettings _settings;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly string _teslaMateBaseUrl;

    public TeslamateApiService(ILogger<TeslamateApiService> logger, ITelegramService telegramService, 
        ISettings settings, IConfigurationWrapper configurationWrapper, IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _telegramService = telegramService;
        _settings = settings;
        _dateTimeProvider = dateTimeProvider;
        _teslaMateBaseUrl = configurationWrapper.TeslaMateApiBaseUrl();
    }

    public async Task StartCharging(int carId, int startAmp, CarStateEnum? carState)
    {
        _logger.LogTrace("{method}({param1}, {param2}, {param3})", nameof(StartCharging), carId, startAmp, carState);
        if (startAmp == 0)
        {
            _logger.LogDebug("Should start charging with 0 amp. Skipping charge start.");
            return;
        }
        await WakeUpCarIfNeeded(carId, carState).ConfigureAwait(false);

        var url = $"{_teslaMateBaseUrl}/api/v1/cars/{carId}/command/charge_start";

        var result = await SendPostToTeslaMate(url).ConfigureAwait(false);

        await SetAmp(carId, startAmp).ConfigureAwait(false);

        _logger.LogTrace("result: {resultContent}", result.Content.ReadAsStringAsync().Result);
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
                await ResumeLogging(carId).ConfigureAwait(false);
                break;
        }
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

        await ResumeLogging(carId).ConfigureAwait(false);

        await Task.Delay(TimeSpan.FromSeconds(20)).ConfigureAwait(false);
    }

    public async Task SetAmp(int carId, int amps)
    {
        _logger.LogTrace("{method}({param1}, {param2})", nameof(SetAmp), carId, amps);
        var car = _settings.Cars.First(c => c.Id == carId);

        var url = $"{_teslaMateBaseUrl}/api/v1/cars/{carId}/command/set_charging_amps";
        var parameters = new Dictionary<string, string>()
        {
            {"charging_amps", amps.ToString()},
        };

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

    /// <summary>
    /// Set charging start time in TeslaApp
    /// </summary>
    /// <param name="carId">TeslaMate car Id</param>
    /// <param name="chargingStartTime">null if no charge should be planned, otherwise time when charge should start.</param>
    /// <returns></returns>
    public async Task SetScheduledCharging(int carId, DateTimeOffset? chargingStartTime)
    {
        _logger.LogTrace("{method}({param1}, {param2})", nameof(SetScheduledCharging), carId, chargingStartTime);
        var car = _settings.Cars.First(c => c.Id == carId);
        var url = $"{_teslaMateBaseUrl}/api/v1/cars/{carId}/command/set_scheduled_charging";

        if (!IsChargingScheduleChangeNeeded(chargingStartTime, _dateTimeProvider.DateTimeOffSetNow(),car, out var parameters))
        {
            _logger.LogDebug("No change in updating scheduled charging needed.");
            return;
        }

        await WakeUpCarIfNeeded(carId, car.CarState.State).ConfigureAwait(false);
        
        var result = await SendPostToTeslaMate(url, parameters).ConfigureAwait(false);
        //assume update was sucessfull as update is not working after mosquitto restart (or wrong cached State)
        if (parameters["enable"] == "false")
        {
            car.CarState.ScheduledChargingStartTime = null;
        }
        _logger.LogTrace("result: {resultContent}", result.Content.ReadAsStringAsync().Result);
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

    private async Task ResumeLogging(int carId)
    {
        _logger.LogTrace("{method}({param1})", nameof(ResumeLogging), carId);
        var url = $"{_teslaMateBaseUrl}/api/v1/cars/{carId}/logging/resume";
        using var httpClient = new HttpClient();
        var response = await httpClient.PutAsync(url, null).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpResponseMessage> SendPostToTeslaMate(string url, Dictionary<string, string>? parameters = null)
    {
        _logger.LogTrace("{method}({param1}, {param2})", nameof(SendPostToTeslaMate), url, parameters);
        var jsonString = JsonConvert.SerializeObject(parameters);
        var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
        _settings.TeslaApiRequestCounter++;
        using var httpClient = new HttpClient();
        var response = await httpClient.PostAsync(url, content).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var responseContentString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            _logger.LogError("Error while sending post to TeslaMate. Response: {response}", responseContentString);
            await _telegramService.SendMessage($"Error while sending post to TeslaMate.\r\n RequestUrl: {url} \r\n RequestBody: {jsonString} \r\n Response: {responseContentString}").ConfigureAwait(false);
        }
        response.EnsureSuccessStatusCode();
        return response;
    }
}
