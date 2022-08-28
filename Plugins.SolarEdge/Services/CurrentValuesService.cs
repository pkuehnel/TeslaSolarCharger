using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Plugins.SolarEdge.Contracts;
using Plugins.SolarEdge.Dtos.CloudApi;

[assembly: InternalsVisibleTo("TeslaSolarCharger.Tests")]
namespace Plugins.SolarEdge.Services;

public class CurrentValuesService : ICurrentValuesService
{
    private readonly ILogger<CurrentValuesService> _logger;
    private readonly SharedValues _sharedValues;
    private readonly IConfiguration _configuration;

    public CurrentValuesService(ILogger<CurrentValuesService> logger, SharedValues sharedValues, IConfiguration configuration)
    {
        _logger = logger;
        _sharedValues = sharedValues;
        _configuration = configuration;
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
        return (int) value;
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
        return (int) batteryPower;
    }

    private async Task<CloudApiValue> GetLatestValue()
    {
        var refreshIntervalInt = _configuration.GetValue<int?>("RefreshIntervalSeconds") ?? 
                                 _configuration.GetValue<int>("RefreshIntervallSeconds");
        var refreshInterval = TimeSpan.FromSeconds(refreshIntervalInt);
        _logger.LogDebug("Refresh Interval is {refreshInterval}", refreshInterval);


        if (_sharedValues.CloudApiValues.Count < 1
            || _sharedValues.CloudApiValues.Last().Key < DateTime.UtcNow - refreshInterval)
        {
            _logger.LogDebug("Get new Values from SolarEdge API");
            var jsonString = await GetCloudApiString().ConfigureAwait(false);
            var cloudApiValue = GetCloudApiValueFromString(jsonString);
            AddCloudApiValueToSharedValues(cloudApiValue);
        }

        var latestValue = _sharedValues.CloudApiValues.Last().Value;
        return latestValue;
    }

    private void AddCloudApiValueToSharedValues(CloudApiValue cloudApiValue)
    {
        _logger.LogTrace("{method}({param1})", nameof(AddCloudApiValueToSharedValues), cloudApiValue);
        var currentDateTime = DateTime.UtcNow;
        _sharedValues.CloudApiValues.Add(currentDateTime, cloudApiValue);
    }

    private async Task<string> GetCloudApiString()
    {
        _logger.LogTrace("{method}()", nameof(GetCloudApiString));
        using var httpClient = new HttpClient();
        var requestUrl = _configuration.GetValue<string>("CloudUrl");
        _logger.LogDebug("Request URL is {requestUrl}", requestUrl);
        var response = await httpClient.GetAsync(requestUrl).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    internal CloudApiValue GetCloudApiValueFromString(string jsonString)
    {
        _logger.LogTrace("{method}({param1}", nameof(GetCloudApiValueFromString), jsonString);
        return JsonConvert.DeserializeObject<CloudApiValue>(jsonString) ?? throw new InvalidOperationException("Can not deserialize CloudApiValue");
    }
}
