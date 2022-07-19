﻿using System.Reflection;
using System.Runtime.Caching;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

[assembly: InternalsVisibleTo("TeslaSolarCharger.Tests")]
namespace TeslaSolarCharger.Shared.Wrappers;

public class ConfigurationWrapper : IConfigurationWrapper
{
    private readonly ILogger<ConfigurationWrapper> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _baseConfigurationMemoryCacheName = "baseConfiguration";

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

    public string BaseConfigFileFullName()
    {
        var configFileDirectory = ConfigFileDirectory();
        var environmentVariableName = "BaseConfigFileName";
        var value = GetNotNullableConfigurationValue<string>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return Path.Combine(configFileDirectory, value);
    }

    internal string ConfigFileDirectory()
    {
        var environmentVariableName = "ConfigFileLocation";
        var value = GetNotNullableConfigurationValue<string>(environmentVariableName);
        _logger.LogDebug("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        var path = new FileInfo(Assembly.GetExecutingAssembly().Location).Directory?.FullName;
        path = Path.Combine(path ?? throw new InvalidOperationException("Could not get Assembly directory"), value);
        return path;
    }

    public TimeSpan ChargingValueJobUpdateIntervall()
    {
        var minimum = TimeSpan.FromSeconds(20);
        var updateIntervalSeconds = GetBaseConfiguration().UpdateIntervalSeconds;
        var value = GetValueIfGreaterThanMinimum(TimeSpan.FromSeconds(updateIntervalSeconds), minimum);
        return value;
    }

    public TimeSpan PvValueJobUpdateIntervall()
    {
        var maximum = ChargingValueJobUpdateIntervall();
        var minimum = TimeSpan.FromSeconds(1);
        var updateIntervalSeconds = GetBaseConfiguration().PvValueUpdateIntervalSeconds;
        var value = TimeSpan.FromSeconds(updateIntervalSeconds ?? ChargingValueJobUpdateIntervall().TotalSeconds);

        if (value > maximum)
        {
            value = maximum;
        } 
        else if (value < minimum)
        {
            value = minimum;
        }
        return value;
    }

    public string MqqtClientId()
    {
        return GetBaseConfiguration().MqqtClientId;
    }

    public string MosquitoServer()
    {
        return GetBaseConfiguration().MosquitoServer;
    }

    public string TeslaMateDbServer()
    {
        return GetBaseConfiguration().TeslaMateDbServer;
    }

    public int TeslaMateDbPort()
    {
        return GetBaseConfiguration().TeslaMateDbPort;
    }

    public string TeslaMateDbDatabaseName()
    {
        return GetBaseConfiguration().TeslaMateDbDatabaseName;
    }

    public string TeslaMateDbUser()
    {
        return GetBaseConfiguration().TeslaMateDbUser;
    }

    public string TeslaMateDbPassword()
    {
        return GetBaseConfiguration().TeslaMateDbPassword;
    }

    public string CurrentPowerToGridUrl()
    {
        return GetBaseConfiguration().CurrentPowerToGridUrl;
    }

    public string? CurrentInverterPowerUrl()
    {
        return GetBaseConfiguration().CurrentInverterPowerUrl;
    }
    
    public string? CurrentPowerToGridJsonPattern()
    {
        return GetBaseConfiguration().CurrentPowerToGridJsonPattern;
    }

    public string? CurrentPowerToGridXmlPattern()
    {
        return GetBaseConfiguration().CurrentPowerToGridXmlPattern;
    }

    public string? CurrentPowerToGridXmlAttributeHeaderName()
    {
        return GetBaseConfiguration().CurrentPowerToGridXmlAttributeHeaderName;
    }

    public string? CurrentPowerToGridXmlAttributeHeaderValue()
    {
        return GetBaseConfiguration().CurrentPowerToGridXmlAttributeHeaderValue;
    }

    public string? CurrentPowerToGridXmlAttributeValueName()
    {
        return GetBaseConfiguration().CurrentPowerToGridXmlAttributeValueName;
    }

    public string? CurrentInverterPowerJsonPattern()
    {
        return GetBaseConfiguration().CurrentInverterPowerJsonPattern;
    }

    public string? CurrentInverterPowerXmlPattern()
    {
        return GetBaseConfiguration().CurrentInverterPowerXmlPattern;
    }

    public string? CurrentInverterPowerXmlAttributeHeaderName()
    {
        return GetBaseConfiguration().CurrentInverterPowerXmlAttributeHeaderName;
    }

    public string? CurrentInverterPowerXmlAttributeHeaderValue()
    {
        return GetBaseConfiguration().CurrentInverterPowerXmlAttributeHeaderValue;
    }

    public string? CurrentInverterPowerXmlAttributeValueName()
    {
        return GetBaseConfiguration().CurrentInverterPowerXmlAttributeValueName;
    }

    public bool CurrentPowerToGridInvertValue()
    {
        return GetBaseConfiguration().CurrentPowerToGridInvertValue;
    }

    public string TeslaMateApiBaseUrl()
    {
        return GetBaseConfiguration().TeslaMateApiBaseUrl;
    }

    public List<int> CarPriorities()
    {
        var rawValue = GetBaseConfiguration().CarPriorities;
        var value = rawValue.Split("|").Select(id => Convert.ToInt32(id)).ToList();
        return value;
    }

    public string GeoFence()
    {
        return GetBaseConfiguration().GeoFence;
    }

    public TimeSpan TimeUntilSwitchOn()
    {
        var rawValue = GetBaseConfiguration().MinutesUntilSwitchOn;
        var timeSpan = TimeSpan.FromMinutes(rawValue);
        var minimum = TimeSpan.FromMinutes(1);
        var value = GetValueIfGreaterThanMinimum(timeSpan, minimum);
        return value;
    }

    public TimeSpan TimespanUntilSwitchOff()
    {
        var rawValue = GetBaseConfiguration().MinutesUntilSwitchOff;
        var timeSpan = TimeSpan.FromMinutes(rawValue);
        var minimum = TimeSpan.FromMinutes(1);
        var value = GetValueIfGreaterThanMinimum(timeSpan, minimum);
        return value;
    }

    public int PowerBuffer()
    {
        return GetBaseConfiguration().PowerBuffer;
    }

    public string? TelegramBotKey()
    {
        return GetBaseConfiguration().TelegramBotKey;
    }

    public string? TelegramChannelId()
    {
        return GetBaseConfiguration().TelegramChannelId;
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

    private DtoBaseConfiguration GetBaseConfiguration()
    {
        return GetBaseConfigurationAsync().GetAwaiter().GetResult();
    }


    public async Task<DtoBaseConfiguration> GetBaseConfigurationAsync()
    {
        _logger.LogTrace("{method}()", nameof(GetBaseConfiguration));
        var jsonFileContent = await BaseConfigurationJsonFileContent();

        var dtoBaseConfiguration = JsonConvert.DeserializeObject<DtoBaseConfiguration>(jsonFileContent);

        if (dtoBaseConfiguration == null)
        {
            throw new ArgumentException($"Could not deserialize {jsonFileContent} to {nameof(DtoBaseConfiguration)}");
        }

        return dtoBaseConfiguration;
    }

    private async Task<string?> BaseConfigurationJsonFileContent()
    {
        var cache = MemoryCache.Default;
        var jsonFileContent = cache[_baseConfigurationMemoryCacheName] as string;
        if (jsonFileContent == null)
        {
            var filePath = BaseConfigFileFullName();
            var cacheItemPolicy = new CacheItemPolicy();
            var filePathList = new List<string>()
            {
                filePath,
            };

            cacheItemPolicy.ChangeMonitors.Add(new HostFileChangeMonitor(filePathList));

            if (File.Exists(filePath))
            {
                jsonFileContent = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);

                cache.Set(_baseConfigurationMemoryCacheName, jsonFileContent, cacheItemPolicy);
            }
        }

        return jsonFileContent;
    }

    public async Task SaveBaseConfiguration(DtoBaseConfiguration baseConfiguration)
    {
        var baseConfigurationBase = (BaseConfigurationBase)baseConfiguration;
        var baseConfigurationJson = JsonConvert.DeserializeObject<BaseConfigurationJson>(JsonConvert.SerializeObject(baseConfigurationBase));
        //if (false)
        //{
        //    baseConfigurationJson.LastEditDateTime = DateTime.UtcNow;
        //}
        var jsonFileContent = JsonConvert.SerializeObject(baseConfigurationJson);

        var configFileLocation = BaseConfigFileFullName();
        var fileInfo = new FileInfo(configFileLocation);
        var configDirectoryFullName = fileInfo.Directory?.FullName;
        if (!Directory.Exists(configDirectoryFullName))
        {
            _logger.LogDebug("Config directory {directoryname} does not exist.", configDirectoryFullName);
            Directory.CreateDirectory(configDirectoryFullName ?? throw new InvalidOperationException());
        }

        await UpdateJsonFile(configFileLocation, jsonFileContent).ConfigureAwait(false);
    }

    private async Task UpdateJsonFile(string configFileLocation, string jsonFileContent)
    {
        await File.WriteAllTextAsync(configFileLocation, jsonFileContent);
        var cache = MemoryCache.Default;
        cache.Remove(_baseConfigurationMemoryCacheName, CacheEntryRemovedReason.ChangeMonitorChanged);
    }

    public async Task<bool> IsBaseConfigurationJsonRelevant()
    {
        var jsonContent = await BaseConfigurationJsonFileContent().ConfigureAwait(false);
        if (jsonContent == null)
        {
            return false;
        }
        var baseConfigurationJson = JsonConvert.DeserializeObject<BaseConfigurationJson>(jsonContent);
        return baseConfigurationJson?.LastEditDateTime != null;
    }

    public async Task UpdateBaseConfigurationAsync(DtoBaseConfiguration dtoBaseConfiguration)
    {
        var baseConfigurationBase = (BaseConfigurationBase)dtoBaseConfiguration;
        var baseConfigurationJson = JsonConvert.DeserializeObject<BaseConfigurationJson>(JsonConvert.SerializeObject(baseConfigurationBase));

        if (baseConfigurationJson == null)
        {
            throw new InvalidOperationException("Could not deserialize dtoBaseConfiguration to baseconfigurationJson");
        }
        baseConfigurationJson.LastEditDateTime = DateTime.UtcNow;

        var baseConfigurationJsonString = JsonConvert.SerializeObject(baseConfigurationJson);

        await UpdateJsonFile(BaseConfigFileFullName(), baseConfigurationJsonString).ConfigureAwait(false);
    }
}