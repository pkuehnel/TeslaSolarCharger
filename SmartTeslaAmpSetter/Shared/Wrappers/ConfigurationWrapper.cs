using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartTeslaAmpSetter.Shared.Contracts;

[assembly: InternalsVisibleTo("SmartTeslaAmpSetter.Tests")]
namespace SmartTeslaAmpSetter.Shared.Wrappers;

public class ConfigurationWrapper : IConfigurationWrapper
{
    private readonly ILogger<ConfigurationWrapper> _logger;
    private readonly IConfiguration _configuration;

    public ConfigurationWrapper(ILogger<ConfigurationWrapper> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public string CarConfigFileFullName()
    {
        var configFileDirectory = ConfigFileDirectory();
        var environmentVariableName = "CarConfigFilename";
        var value = GetNotNullableConfigurationValue<string>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return Path.Combine(configFileDirectory, value);
    }

    internal string ConfigFileDirectory()
    {
        var environmentVariableName = "ConfigFileLocation";
        var value = GetNotNullableConfigurationValue<string>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public TimeSpan ChargingValueJobUpdateIntervall()
    {
        var environmentVariableName = "UpdateIntervallSeconds";
        var minimum = TimeSpan.FromSeconds(20);
        var value = GetSecondsConfigurationValueIfGreaterThanMinumum(environmentVariableName, minimum);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public TimeSpan PvValueJobUpdateIntervall()
    {
        var environmentVariableName = "PvValueUpdateIntervalSeconds";
        var maximum = ChargingValueJobUpdateIntervall();
        var minimum = TimeSpan.FromSeconds(1);
        var value = TimeSpan.FromSeconds(_configuration.GetValue<int>(environmentVariableName));

        if (value > maximum)
        {
            value = maximum;
        } 
        else if (value < minimum)
        {
            value = minimum;
        }
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public string MqqtClientId()
    {
        var environmentVariableName = "MqqtClientId";
        var value = GetNotNullableConfigurationValue<string>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public string MosquitoServer()
    {
        var environmentVariableName = "MosquitoServer";
        var value = GetNotNullableConfigurationValue<string>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public string TeslaMateDbServer()
    {
        var environmentVariableName = "TeslaMateDbServer";
        var value = _configuration.GetValue<string>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public int TeslaMateDbPort()
    {
        var environmentVariableName = "TeslaMateDbPort";
        var value = _configuration.GetValue<int>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public string TeslaMateDbDatabaseName()
    {
        var environmentVariableName = "TeslaMateDbDatabaseName";
        var value = _configuration.GetValue<string>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public string TeslaMateDbUser()
    {
        var environmentVariableName = "TeslaMateDbUser";
        var value = _configuration.GetValue<string>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public string TeslaMateDbPassword()
    {
        var environmentVariableName = "TeslaMateDbPassword";
        var value = _configuration.GetValue<string>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public string CurrentPowerToGridUrl()
    {
        var environmentVariableName = "CurrentPowerToGridUrl";
        var value = GetNotNullableConfigurationValue<string>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public string? CurrentInverterPowerUrl()
    {
        var environmentVariableName = "CurrentInverterPowerUrl";
        var value = GetNullableConfigurationValue<string>(environmentVariableName);
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

    public string? CurrentPowerToGridXmlPattern()
    {
        var environmentVariableName = "CurrentPowerToGridXmlPattern";
        var value = _configuration.GetValue<string>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public string? CurrentPowerToGridXmlAttributeHeaderName()
    {
        var environmentVariableName = "CurrentPowerToGridXmlAttributeHeaderName";
        var value = _configuration.GetValue<string>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public string? CurrentPowerToGridXmlAttributeHeaderValue()
    {
        var environmentVariableName = "CurrentPowerToGridXmlAttributeHeaderValue";
        var value = _configuration.GetValue<string>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public string? CurrentPowerToGridXmlAttributeValueName()
    {
        var environmentVariableName = "CurrentPowerToGridXmlAttributeValueName";
        var value = _configuration.GetValue<string>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public string? CurrentInverterPowerJsonPattern()
    {
        var environmentVariableName = "CurrentInverterPowerJsonPattern";
        var value = _configuration.GetValue<string>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public string? CurrentInverterPowerXmlPattern()
    {
        var environmentVariableName = "CurrentInverterPowerXmlPattern";
        var value = _configuration.GetValue<string>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public string? CurrentInverterPowerXmlAttributeHeaderName()
    {
        var environmentVariableName = "CurrentInverterPowerXmlAttributeHeaderName";
        var value = _configuration.GetValue<string>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public string? CurrentInverterPowerXmlAttributeHeaderValue()
    {
        var environmentVariableName = "CurrentInverterPowerXmlAttributeHeaderValue";
        var value = _configuration.GetValue<string>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public string? CurrentInverterPowerXmlAttributeValueName()
    {
        var environmentVariableName = "CurrentInverterPowerXmlAttributeValueName";
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
        var value = GetNotNullableConfigurationValue<string>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public List<int> CarPriorities()
    {
        var environmentVariableName = "CarPriorities";
        var rawValue = GetNotNullableConfigurationValue<string>(environmentVariableName);
        var value = rawValue.Split("|").Select(id => Convert.ToInt32(id)).ToList();
        _logger.LogDebug("Config value extracted: [{key}]: {@value}", environmentVariableName, value);
        return value;
    }

    public string GeoFence()
    {
        var environmentVariableName = "GeoFence";
        var value = GetNotNullableConfigurationValue<string>(environmentVariableName);
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

    internal T GetNotNullableConfigurationValue<T>(string environmentVariableName)
    {
        var value = GetNullableConfigurationValue<T>(environmentVariableName);
        if (value == null)
        {
            var exception =
                new NullReferenceException($"Configuration value {environmentVariableName} is null or empty");
            _logger.LogError(exception, "Error getting configuration value");
            throw exception;
        }
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    internal T? GetNullableConfigurationValue<T>(string environmentVariableName)
    {
        return _configuration.GetValue<T>(environmentVariableName);
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