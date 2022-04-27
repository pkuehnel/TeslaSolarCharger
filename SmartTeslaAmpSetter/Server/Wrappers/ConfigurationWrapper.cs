using System.Runtime.CompilerServices;
using SmartTeslaAmpSetter.Server.Contracts;

[assembly: InternalsVisibleTo("SmartTeslaAmpSetter.Tests")]
namespace SmartTeslaAmpSetter.Server.Wrappers;

public class ConfigurationWrapper : IConfigurationWrapper
{
    private readonly ILogger<ConfigurationWrapper> _logger;
    private readonly IConfiguration _configuration;

    public ConfigurationWrapper(ILogger<ConfigurationWrapper> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public string ConfigFileLocation()
    {
        var environmentVariableName = "ConfigFileLocation";
        var value = GetNotNullableConfigurationValue(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public TimeSpan UpdateIntervall()
    {
        var environmentVariableName = "UpdateIntervallSeconds";
        var minimum = TimeSpan.FromSeconds(20);
        var value = GetSecondsConfigurationValueIfGreaterThanMinumum(environmentVariableName, minimum);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }
    
    public string MqqtClientId()
    {
        var environmentVariableName = "MqqtClientId";
        var value = GetNotNullableConfigurationValue(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public string MosquitoServer()
    {
        var environmentVariableName = "MosquitoServer";
        var value = GetNotNullableConfigurationValue(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public string CurrentPowerToGridUrl()
    {
        var environmentVariableName = "CurrentPowerToGridUrl";
        var value = GetNotNullableConfigurationValue(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public string? CurrentInverterPowerUrl()
    {
        var environmentVariableName = "CurrentInverterPowerUrl";
        var value = GetNullableConfigurationValue(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }
    
    public string? CurrentPowerToGridJsonPattern()
    {
        var environmentVariableName = "CurrentPowerToGridJsonPattern";
        var value = _configuration.GetValue<string>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public bool CurrentPowerToGridInvertValue()
    {
        var environmentVariableName = "CurrentPowerToGridInvertValue";
        var value = _configuration.GetValue<bool>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public string TeslaMateApiBaseUrl()
    {
        var environmentVariableName = "TeslaMateApiBaseUrl";
        var value = GetNotNullableConfigurationValue(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public List<int> CarPriorities()
    {
        var environmentVariableName = "CarPriorities";
        var rawValue = GetNotNullableConfigurationValue(environmentVariableName);
        var value = rawValue.Split("|").Select(id => Convert.ToInt32(id)).ToList();
        _logger.LogDebug("Config value extracted: [{key}]: {@value}", environmentVariableName, value);
        return value;
    }

    public string GeoFence()
    {
        var environmentVariableName = "GeoFence";
        var value = GetNotNullableConfigurationValue(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public TimeSpan TimeUntilSwitchOn()
    {
        var environmentVariableName = "MinutesUntilSwitchOn";
        var minimum = TimeSpan.FromMinutes(1);
        var value = GetMinutesConfigurationValueIfGreaterThanMinumum(environmentVariableName, minimum);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public TimeSpan TimespanUntilSwitchOff()
    {
        var environmentVariableName = "MinutesUntilSwitchOn";
        var minimum = TimeSpan.FromMinutes(1);
        var value = GetMinutesConfigurationValueIfGreaterThanMinumum(environmentVariableName, minimum);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public int PowerBuffer()
    {
        var environmentVariableName = "PowerBuffer";
        var value = _configuration.GetValue<int>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public string? TelegramBotKey()
    {
        var environmentVariableName = "TelegramBotKey";
        var value = _configuration.GetValue<string>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public string? TelegramChannelId()
    {
        var environmentVariableName = "TelegramChannelId";
        var value = _configuration.GetValue<string>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
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
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
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

    private TimeSpan GetValueIfGreaterThanMinimum(TimeSpan value, TimeSpan minimum)
    {
        if (value < minimum)
        {
            _logger.LogTrace("Replace value {value} with minumum value {minimum}", value, minimum);
            return minimum;
        }
        else
        {
            return value;
        }
    }
}