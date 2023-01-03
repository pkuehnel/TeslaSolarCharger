using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Plugins.SolarEdge.Contracts;
using Plugins.SolarEdge.Dtos.CloudApi;
using System.Net;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;

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

        return latestValue.SiteCurrentPowerFlow.Storage.ChargeLevel;
    }

    public async Task<int> GetHomeBatteryPower()
    {
        _logger.LogTrace("{method}()", nameof(GetHomeBatteryPower));
        var latestValue = await GetLatestValue().ConfigureAwait(false);
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

    private async Task<CloudApiValue> GetLatestValue()
    {
        _logger.LogDebug("Get new Values from SolarEdge API");
        var jsonString = string.Empty;
        try
        {
            jsonString = await GetCloudApiString().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not get json string from solarEdge.");
            return FakeLastValue();
        }
        if (string.IsNullOrEmpty(jsonString))
        {
            return FakeLastValue();
        }
        var cloudApiValue = GetCloudApiValueFromString(jsonString);
        AddCloudApiValueToSharedValues(cloudApiValue);

        var latestValue = _sharedValues.CloudApiValues.Last().Value;
        return latestValue;
    }

    private CloudApiValue FakeLastValue()
    {
        _logger.LogTrace("{method}()", nameof(FakeLastValue));
        var fakedValue = _sharedValues.CloudApiValues.Last().Value;
        fakedValue.SiteCurrentPowerFlow.Grid.CurrentPower = 0;
        fakedValue.SiteCurrentPowerFlow.Pv.CurrentPower = 0;
        fakedValue.SiteCurrentPowerFlow.Storage.CurrentPower = 0;
        return fakedValue;
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
        
        var tooManyRequestsResetMinutesEnvironmentVariableName = "TooManyRequestsResetMinutes";
        var solarEdgeTooManyRequestsResetMinutes = _configuration.GetValue<int>(tooManyRequestsResetMinutesEnvironmentVariableName);
        if (solarEdgeTooManyRequestsResetMinutes == default)
        {
            var defaultResetMinutes = 16;
            _logger.LogDebug("No environmentvariable {envVariableName} found, using default of {defaultValue}", tooManyRequestsResetMinutesEnvironmentVariableName, defaultResetMinutes);
            solarEdgeTooManyRequestsResetMinutes = defaultResetMinutes;
        }
        var solarEdgeTooManyRequestsResetTime = TimeSpan.FromMinutes(solarEdgeTooManyRequestsResetMinutes);
        var numberOfRelevantCars = await GetNumberOfRelevantCars().ConfigureAwait(false);
        if ((_sharedValues.LastTooManyRequests < (_dateTimeProvider.Now() + solarEdgeTooManyRequestsResetTime)) || numberOfRelevantCars < 1)
        {
            _logger.LogDebug("Prevent calling SolarEdge API as last too many requests error is from {lastTooManyRequestError} and relevantCarCount is {relevantCarCount}", _sharedValues.LastTooManyRequests, numberOfRelevantCars);
            return null;
        }
        var requestUrl = _configuration.GetValue<string>("CloudUrl");
        _logger.LogDebug("Request URL is {requestUrl}", requestUrl);
        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(requestUrl).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _sharedValues.LastTooManyRequests = _dateTimeProvider.Now();
        }
        else
        {
            _sharedValues.LastTooManyRequests = null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    private async Task<int> GetNumberOfRelevantCars()
    {
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(1);
        try
        {
            var requestUrl = "http://teslsolarcharger/api/Hello/NumberOfRelevantCars";
            var numberOfRelevantCars = await httpClient.GetFromJsonAsync<DtoValue<int>>(requestUrl).ConfigureAwait(false);
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
        

        _logger.LogTrace("Number of relevant cars could not be determined, use default value 1");
        return 1;
    }

    internal CloudApiValue GetCloudApiValueFromString(string jsonString)
    {
        _logger.LogTrace("{method}({param1}", nameof(GetCloudApiValueFromString), jsonString);
        return JsonConvert.DeserializeObject<CloudApiValue>(jsonString) ?? throw new InvalidOperationException("Can not deserialize CloudApiValue");
    }
}
