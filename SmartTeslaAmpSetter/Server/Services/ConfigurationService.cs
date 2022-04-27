using System.Runtime.CompilerServices;
using SmartTeslaAmpSetter.Server.Contracts;

[assembly: InternalsVisibleTo("SmartTeslaAmpSetter.Tests")]
namespace SmartTeslaAmpSetter.Server.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    private readonly IConfiguration _configuration;

    public ConfigurationService(ILogger<ConfigurationService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public string ConfigFileLocation()
    {
        var environmentVariableName = "ConfigFileLocation";
        var value = GetNotNullableConfigurationValue(environmentVariableName);
        return value;
    }

    public TimeSpan UpdateIntervall()
    {
        var environmentVariableName = "UpdateIntervallSeconds";
        var minimum = TimeSpan.FromSeconds(20);
        return GetSecondsConfigurationValueIfGreaterThanMinumum(environmentVariableName, minimum);
    }
    
    public string MqqtClientId()
    {
        var environmentVariableName = "MqqtClientId";
        var value = GetNotNullableConfigurationValue(environmentVariableName);
        return value;
    }

    public string MosquitoServer()
    {
        var environmentVariableName = "MosquitoServer";
        var value = GetNotNullableConfigurationValue(environmentVariableName);
        return value;
    }

    public string CurrentPowerToGridUrl()
    {
        var environmentVariableName = "CurrentPowerToGridUrl";
        var value = GetNotNullableConfigurationValue(environmentVariableName);
        return value;
    }

    public string? CurrentInverterPowerUrl()
    {
        var environmentVariableName = "CurrentInverterPowerUrl";
        return GetNullableConfigurationValue(environmentVariableName);
    }
    
    public string? CurrentPowerToGridJsonPattern()
    {
        return _configuration.GetValue<string>("CurrentPowerToGridJsonPattern");
    }

    public bool CurrentPowerToGridInvertValue()
    {
        return _configuration.GetValue<bool>("CurrentPowerToGridInvertValue");
    }

    public string TeslaMateApiBaseUrl()
    {
        var environmentVariableName = "TeslaMateApiBaseUrl";
        var value = GetNotNullableConfigurationValue(environmentVariableName);
        return value;
    }

    public List<int> CarPriorities()
    {
        var environmentVariableName = "CarPriorities";
        var value = GetNotNullableConfigurationValue(environmentVariableName);
        return value.Split("|").Select(id => Convert.ToInt32(id)).ToList();
    }

    public string GeoFence()
    {
        var environmentVariableName = "GeoFence";
        var value = GetNotNullableConfigurationValue(environmentVariableName);
        return value;
    }

    public TimeSpan TimeUntilSwitchOn()
    {
        var environmentVariableName = "MinutesUntilSwitchOn";
        var minimum = TimeSpan.FromMinutes(1);
        return GetMinutesConfigurationValueIfGreaterThanMinumum(environmentVariableName, minimum);
    }

    public TimeSpan MinutesUntilSwitchOff()
    {
        var environmentVariableName = "MinutesUntilSwitchOn";
        var minimum = TimeSpan.FromMinutes(1);
        return GetMinutesConfigurationValueIfGreaterThanMinumum(environmentVariableName, minimum);
    }

    public int PowerBuffer()
    {
        return _configuration.GetValue<int>("PowerBuffer");
    }

    internal string GetNotNullableConfigurationValue(string environmentVariableName)
    {
        var value = GetNullableConfigurationValue(environmentVariableName);
        if (string.IsNullOrEmpty(value))
        {
            var exception =
                new NullReferenceException($"Configuration value {environmentVariableName} is null or empty");
            _logger.LogError(exception, "Error getting configuration value");
            throw exception;
        }

        return value;
    }

    internal string? GetNullableConfigurationValue(string environmentVariableName)
    {
        return _configuration.GetValue<string>(environmentVariableName);
    }

    internal TimeSpan GetSecondsConfigurationValueIfGreaterThanMinumum(string environmentVariableName, TimeSpan minimum)
    {
        var value = TimeSpan.FromSeconds(_configuration.GetValue<int>(environmentVariableName));
        return GetValueIfGreaterThanMinimum(value, minimum);
    }

    internal TimeSpan GetMinutesConfigurationValueIfGreaterThanMinumum(string environmentVariableName, TimeSpan minimum)
    {
        var value = TimeSpan.FromMinutes(_configuration.GetValue<int>(environmentVariableName));
        return GetValueIfGreaterThanMinimum(value, minimum);
    }

    private static TimeSpan GetValueIfGreaterThanMinimum(TimeSpan value, TimeSpan minimum)
    {
        return value < minimum ? minimum : value;
    }
}