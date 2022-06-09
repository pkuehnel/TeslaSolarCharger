using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Plugins.SolarEdge.Contracts;
using Plugins.SolarEdge.Dtos.CloudApi;

[assembly: InternalsVisibleTo("SmartTeslaAmpSetter.Tests")]
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
        var latestValue = await GetLatestValue();

        var value = (int)latestValue.SiteCurrentPowerFlow.Grid.CurrentPower;
        if (latestValue.SiteCurrentPowerFlow.Unit == "kW")
        {
            value *= 1000;
        }

        if (latestValue.SiteCurrentPowerFlow.Connections.Any(c => c.From == "GRID"))
        {
            value = -value;
        }
        return value;
    }

    public async Task<int> GetInverterPower()
    {
        _logger.LogTrace("{method}()", nameof(GetInverterPower));
        var latestValue = await GetLatestValue();

        if (latestValue.SiteCurrentPowerFlow.Unit == "kW")
        {
            return (int)(latestValue.SiteCurrentPowerFlow.Pv.CurrentPower * 1000);
        }

        return (int)latestValue.SiteCurrentPowerFlow.Pv.CurrentPower;
    }

    private async Task<CloudApiValue> GetLatestValue()
    {
        var refreshIntervall = TimeSpan.FromSeconds(_configuration.GetValue<int>("RefreshIntervallSeconds"));
        _logger.LogDebug("Refresh Intervall is {refreshIntervall}", refreshIntervall);


        if (_sharedValues.CloudApiValues.Count < 1
            || _sharedValues.CloudApiValues.Last().Key < DateTime.UtcNow - refreshIntervall)
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