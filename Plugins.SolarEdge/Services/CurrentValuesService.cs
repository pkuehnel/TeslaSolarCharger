using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Plugins.SolarEdge.Contracts;
using Plugins.SolarEdge.Dtos.CloudApi;
using System.Net;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.SharedBackend.Dtos;

[assembly: InternalsVisibleTo("TeslaSolarCharger.Tests")]
namespace Plugins.SolarEdge.Services;

public class CurrentValuesService : ICurrentValuesService
{
    private readonly ILogger<CurrentValuesService> _logger;
    private readonly SharedValues _sharedValues;
    private readonly IConfiguration _configuration;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CurrentValuesService(ILogger<CurrentValuesService> logger, SharedValues sharedValues, IConfiguration configuration,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _sharedValues = sharedValues;
        _configuration = configuration;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<int> GetCurrentPowerToGrid()
    {
        _logger.LogTrace("{method}()", nameof(GetCurrentPowerToGrid));
        var latestValue = await GetLatestValue().ConfigureAwait(false);

        return GetGridPowerFromLatestValue(latestValue);
    }

    private static int GetGridPowerFromLatestValue(CloudApiValue latestValue)
    {
        var value = latestValue.SiteCurrentPowerFlow.Grid.CurrentPower;
        if (latestValue.SiteCurrentPowerFlow.Unit == "kW")
        {
            value *= 1000;
        }

        if (latestValue.SiteCurrentPowerFlow.Connections.Any(c => c.From == "GRID"))
        {
            value = -value;
        }

        return (int)value;
    }

    public async Task<int> GetInverterPower()
    {
        _logger.LogTrace("{method}()", nameof(GetInverterPower));
        var latestValue = await GetLatestValue().ConfigureAwait(false);

        return GetInverterPowerFromLatestValue(latestValue);
    }

    private static int GetInverterPowerFromLatestValue(CloudApiValue latestValue)
    {
        if (latestValue.SiteCurrentPowerFlow.Unit == "kW")
        {
            return (int)(latestValue.SiteCurrentPowerFlow.Pv.CurrentPower * 1000);
        }

        return (int)latestValue.SiteCurrentPowerFlow.Pv.CurrentPower;
    }

    public async Task<int> GetHomeBatterySoc()
    {
        _logger.LogTrace("{method}()", nameof(GetHomeBatterySoc));
        var latestValue = await GetLatestValue().ConfigureAwait(false);

        return GetHomeBatterySocFromLatestValue(latestValue);
    }

    private static int GetHomeBatterySocFromLatestValue(CloudApiValue latestValue)
    {
        return latestValue.SiteCurrentPowerFlow.Storage.ChargeLevel;
    }

    public async Task<int> GetHomeBatteryPower()
    {
        _logger.LogTrace("{method}()", nameof(GetHomeBatteryPower));
        var latestValue = await GetLatestValue().ConfigureAwait(false);
        return GetHomeBatteryPowerFromLatestValue(latestValue);
    }

    private static int GetHomeBatteryPowerFromLatestValue(CloudApiValue latestValue)
    {
        var batteryPower = latestValue.SiteCurrentPowerFlow.Storage.CurrentPower;
        if (string.Equals(latestValue.SiteCurrentPowerFlow.Storage.Status, "Discharging"))
        {
            batteryPower = -batteryPower;
        }

        if (latestValue.SiteCurrentPowerFlow.Unit == "kW")
        {
            return (int)(batteryPower * 1000);
        }

        return (int)batteryPower;
    }

    public async Task<DtoCurrentPvValues> GetCurrentPvValues()
    {
        _logger.LogTrace("{method}()", nameof(GetCurrentPvValues));
        var latestValue = await GetLatestValue().ConfigureAwait(false);
        var currentPvValues = new DtoCurrentPvValues()
        {
            GridPower = GetGridPowerFromLatestValue(latestValue),
            InverterPower = GetInverterPowerFromLatestValue(latestValue),
            HomeBatteryPower = GetHomeBatteryPowerFromLatestValue(latestValue),
            HomeBatterySoc = GetHomeBatterySocFromLatestValue(latestValue),
            LastUpdated = _sharedValues.CloudApiValues.OrderBy(c => c.Key).LastOrDefault().Key,
        };
        return currentPvValues;
    }

    private async Task<CloudApiValue> GetLatestValue()
    {
        _logger.LogTrace("{method}()", nameof(GetLatestValue));
        string? jsonString;
        try
        {
            jsonString = await GetCloudApiString().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not get json string from solarEdge.");
            return await FakeLastValue().ConfigureAwait(false);
        }
        if (string.IsNullOrEmpty(jsonString))
        {
            return await FakeLastValue().ConfigureAwait(false);
        }
        var cloudApiValue = GetCloudApiValueFromString(jsonString);
        AddCloudApiValueToSharedValues(cloudApiValue);

        var latestValue = _sharedValues.CloudApiValues.Last().Value;
        return latestValue;
    }

    private async Task<CloudApiValue> FakeLastValue()
    {
        _logger.LogTrace("{method}()", nameof(FakeLastValue));
        var fakedValue = _sharedValues.CloudApiValues.Last().Value;
        fakedValue.SiteCurrentPowerFlow.Grid.CurrentPower = 0;
        fakedValue.SiteCurrentPowerFlow.Storage.Status = "Charging";
        var targetBatteryChargePower = await GetTargetBatteryPower().ConfigureAwait(false);
        fakedValue.SiteCurrentPowerFlow.Storage.CurrentPower = targetBatteryChargePower / 1000.0;
        return fakedValue;
    }

    private async Task<int> GetTargetBatteryPower()
    {
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(1);
        var teslaSolarChargerHost = GetTeslaSolarChargerHost();
        var requestUrl = $"http://{teslaSolarChargerHost}/api/Hello/HomeBatteryTargetChargingPower";
        _logger.LogTrace("RequestUrl: {requestUrl}", requestUrl);
        var response = await httpClient.GetAsync(requestUrl).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var targetBatteryPower = JsonConvert.DeserializeObject<DtoValue<int>>(responseContent);
        if (targetBatteryPower != null)
        {
            return targetBatteryPower.Value;
        }
        return 0;
    }

    private string GetTeslaSolarChargerHost()
    {
        var teslaSolarChargerHostEnvironmentVariableName = "TeslaSolarChargerHost";
        var teslaSolarChargerHost = _configuration.GetValue<string>(teslaSolarChargerHostEnvironmentVariableName);
        if (string.IsNullOrEmpty(teslaSolarChargerHost))
        {
            teslaSolarChargerHost = "teslasolarcharger";
        }
        return teslaSolarChargerHost;
    }

    private void AddCloudApiValueToSharedValues(CloudApiValue cloudApiValue)
    {
        _logger.LogTrace("{method}({param1})", nameof(AddCloudApiValueToSharedValues), cloudApiValue);
        var currentDateTime = _dateTimeProvider.UtcNow();
        _sharedValues.CloudApiValues.Add(currentDateTime, cloudApiValue);
    }

    private async Task<string?> GetCloudApiString()
    {
        _logger.LogTrace("{method}()", nameof(GetCloudApiString));
        
        var solarEdgeTooManyRequestsResetTime = GetSolarEdgeRequestResetTimeSpan();
        var numberOfRelevantCars = await GetNumberOfRelevantCars().ConfigureAwait(false);
        //Never call SolarEdge API if there was a TooManyRequests Status within the last request Reset time. This could result in errors after restarts
        if (_sharedValues.LastTooManyRequests > (_dateTimeProvider.UtcNow() - solarEdgeTooManyRequestsResetTime))
        {
            _logger.LogDebug("Prevent calling SolarEdge API as last too many requests error is from {lastTooManyRequestError}", _sharedValues.LastTooManyRequests);
            return null;
        }
        //If there are already values there and there is no relevant car, call API everytime reset minutes are over.
        if (_sharedValues.CloudApiValues.Count > 0 && numberOfRelevantCars < 1 && _sharedValues.CloudApiValues.MaxBy(v => v.Key).Key > DateTime.UtcNow - solarEdgeTooManyRequestsResetTime)
        {
            _logger.LogDebug("Prevent calling SolarEdge API as relevantCarCount is {relevantCarCount}", numberOfRelevantCars);
            return null;
        }
        var requestUrl = _configuration.GetValue<string>("CloudUrl");
        _logger.LogDebug("Request URL is {requestUrl}", requestUrl);
        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(requestUrl).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _sharedValues.LastTooManyRequests = _dateTimeProvider.UtcNow();
        }
        else
        {
            _sharedValues.LastTooManyRequests = null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    private TimeSpan GetSolarEdgeRequestResetTimeSpan()
    {
        var tooManyRequestsResetMinutesEnvironmentVariableName = "TooManyRequestsResetMinutes";
        var solarEdgeTooManyRequestsResetMinutes =
            _configuration.GetValue<int>(tooManyRequestsResetMinutesEnvironmentVariableName);
        if (solarEdgeTooManyRequestsResetMinutes == default)
        {
            var defaultResetMinutes = 16;
            _logger.LogDebug("No environmentvariable {envVariableName} found, using default of {defaultValue}",
                tooManyRequestsResetMinutesEnvironmentVariableName, defaultResetMinutes);
            solarEdgeTooManyRequestsResetMinutes = defaultResetMinutes;
        }

        var solarEdgeTooManyRequestsResetTime = TimeSpan.FromMinutes(solarEdgeTooManyRequestsResetMinutes);
        return solarEdgeTooManyRequestsResetTime;
    }

    private async Task<int> GetNumberOfRelevantCars()
    {
        _logger.LogTrace("{method}()", nameof(GetNumberOfRelevantCars));
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(1);
        try
        {
            var teslaSolarChargerHost = GetTeslaSolarChargerHost();
            var requestUrl = $"http://{teslaSolarChargerHost}/api/Hello/NumberOfRelevantCars";
            _logger.LogTrace("RequestUrl: {requestUrl}", requestUrl);
            var response = await httpClient.GetAsync(requestUrl).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var numberOfRelevantCars = JsonConvert.DeserializeObject<DtoValue<int>>(responseContent);
            if (numberOfRelevantCars != null)
            {
                return numberOfRelevantCars.Value;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not access number of relevant cars, use 1");
            return 1;
        }
        

        _logger.LogInformation("Number of relevant cars could not be determined, use default value 1");
        return 1;
    }

    internal CloudApiValue GetCloudApiValueFromString(string jsonString)
    {
        _logger.LogTrace("{method}({param1}", nameof(GetCloudApiValueFromString), jsonString);
        return JsonConvert.DeserializeObject<CloudApiValue>(jsonString) ?? throw new InvalidOperationException("Can not deserialize CloudApiValue");
    }
}
