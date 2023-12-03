using System.Reflection;
using System.Runtime.Caching;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;

[assembly: InternalsVisibleTo("TeslaSolarCharger.Tests")]
namespace TeslaSolarCharger.Shared.Wrappers;

public class ConfigurationWrapper : IConfigurationWrapper
{
    private readonly ILogger<ConfigurationWrapper> _logger;
    private readonly IConfiguration _configuration;
    private readonly INodePatternTypeHelper _nodePatternTypeHelper;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISettings _settings;
    private readonly string _baseConfigurationMemoryCacheName = "baseConfiguration";

    public ConfigurationWrapper(ILogger<ConfigurationWrapper> logger, IConfiguration configuration, INodePatternTypeHelper nodePatternTypeHelper,
        IDateTimeProvider dateTimeProvider, ISettings settings)
    {
        _logger = logger;
        _configuration = configuration;
        _nodePatternTypeHelper = nodePatternTypeHelper;
        _dateTimeProvider = dateTimeProvider;
        _settings = settings;
    }

    public string CarConfigFileFullName()
    {
        var configFileDirectory = ConfigFileDirectory();
        var environmentVariableName = "CarConfigFilename";
        var value = GetNotNullableConfigurationValue<string>(environmentVariableName);
        _logger.LogTrace("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return Path.Combine(configFileDirectory, value);
    }

    public string BackupCopyDestinationDirectory()
    {
        var configFileDirectory = ConfigFileDirectory();
        var environmentVariableName = "BackupCopyDestinationDirectory";
        var value = GetNotNullableConfigurationValue<string>(environmentVariableName);
        _logger.LogTrace("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return Path.Combine(configFileDirectory, value);
    }

    public string BackupZipDirectory()
    {
        var configFileDirectory = ConfigFileDirectory();
        var environmentVariableName = "BackupZipDirectory";
        var value = GetNotNullableConfigurationValue<string>(environmentVariableName);
        _logger.LogTrace("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return Path.Combine(configFileDirectory, value);
    }

    public string SqliteFileFullName()
    {
        var configFileDirectory = ConfigFileDirectory();
        var value = GetSqliteFileNameWithoutPath();
        return Path.Combine(configFileDirectory, value);
    }

    public string GetSqliteFileNameWithoutPath()
    {
        var environmentVariableName = "SqliteFileName";
        var value = GetNotNullableConfigurationValue<string>(environmentVariableName);
        _logger.LogTrace("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return value;
    }

    public string BaseConfigFileFullName()
    {
        var configFileDirectory = ConfigFileDirectory();
        var environmentVariableName = "BaseConfigFileName";
        var value = GetNotNullableConfigurationValue<string>(environmentVariableName);
        _logger.LogTrace("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        return Path.Combine(configFileDirectory, value);
    }

    internal string ConfigFileDirectory()
    {
        var environmentVariableName = "ConfigFileLocation";
        var value = GetNotNullableConfigurationValue<string>(environmentVariableName);
        _logger.LogTrace("Config value extracted: [{key}]: {value}", environmentVariableName, value);
        var path = new FileInfo(Assembly.GetExecutingAssembly().Location).Directory?.FullName;
        path = Path.Combine(path ?? throw new InvalidOperationException("Could not get Assembly directory"), value);
        return path;
    }

    public bool AllowCors()
    {
        var environmentVariableName = "AllowCORS";
        var value = _configuration.GetValue<bool>(environmentVariableName);
        return value;
    }

    public bool UseFleetApi()
    {
        var environmentVariableName = "UseFleetApi";
        var value = _configuration.GetValue<bool>(environmentVariableName);
        return value;
    }

    public string BackendApiBaseUrl()
    {
        var environmentVariableName = "BackendApiBaseUrl";
        var value = _configuration.GetValue<string>(environmentVariableName);
        return value;
    }

    public string FleetApiClientId()
    {
        var environmentVariableName = "FleetApiClientId";
        var value = GetNotNullableConfigurationValue<string>(environmentVariableName);
        return value;
    }

    public TimeSpan ChargingValueJobUpdateIntervall()
    {
        var minimum = TimeSpan.FromSeconds(20);
        var pvValueUpdateIntervalSeconds = GetBaseConfiguration().PvValueUpdateIntervalSeconds ?? 1;
        if (minimum.TotalSeconds < pvValueUpdateIntervalSeconds)
        {
            minimum = TimeSpan.FromSeconds(pvValueUpdateIntervalSeconds);
        }
        var updateIntervalSeconds = GetBaseConfiguration().UpdateIntervalSeconds;
        var value = GetValueIfGreaterThanMinimum(TimeSpan.FromSeconds(updateIntervalSeconds), minimum);
        return value;
    }

    public TimeSpan PvValueJobUpdateIntervall()
    {
        var minimum = TimeSpan.FromSeconds(1);
        var updateIntervalSeconds = GetBaseConfiguration().PvValueUpdateIntervalSeconds;
        var value = TimeSpan.FromSeconds(updateIntervalSeconds ?? ChargingValueJobUpdateIntervall().TotalSeconds);

        if (value < minimum)
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

    public string? CurrentPowerToGridUrl()
    {
        return GetBaseConfiguration().CurrentPowerToGridUrl;
    }

    public string? SolarMqttServer()
    {
        return GetBaseConfiguration().SolarMqttServer;
    }

    public string? SolarMqttUsername()
    {
        return GetBaseConfiguration().SolarMqttUserName;
    }

    public string? SolarMqttPassword()
    {
        return GetBaseConfiguration().SolarMqttPassword;
    }

    public string? CurrentPowerToGridMqttTopic()
    {
        return GetBaseConfiguration().CurrentPowerToGridMqttTopic;
    }

    public Dictionary<string, string> CurrentPowerToGridHeaders()
    {
        return GetBaseConfiguration().CurrentPowerToGridHeaders;
    }

    public string? CurrentInverterPowerMqttTopic()
    {
        return GetBaseConfiguration().CurrentInverterPowerMqttTopic;
    }

    public string? CurrentInverterPowerUrl()
    {
        return GetBaseConfiguration().CurrentInverterPowerUrl;
    }
    public Dictionary<string, string> CurrentInverterPowerHeaders()
    {
        return GetBaseConfiguration().CurrentInverterPowerHeaders;
    }

    public string? HomeBatterySocUrl()
    {
        return GetBaseConfiguration().HomeBatterySocUrl;
    }

    public string? HomeBatterySocMqttTopic()
    {
        return GetBaseConfiguration().HomeBatterySocMqttTopic;
    }

    public Dictionary<string, string> HomeBatterySocHeaders()
    {
        return GetBaseConfiguration().HomeBatterySocHeaders;
    }

    public string? HomeBatteryPowerMqttTopic()
    {
        return GetBaseConfiguration().HomeBatteryPowerMqttTopic;
    }

    public string? HomeBatteryPowerUrl()
    {
        return GetBaseConfiguration().HomeBatteryPowerUrl;
    }

    public string? HomeBatteryPowerInversionUrl()
    {
        return GetBaseConfiguration().HomeBatteryPowerInversionUrl;
    }

    public Dictionary<string, string> HomeBatteryPowerHeaders()
    {
        return GetBaseConfiguration().HomeBatteryPowerHeaders;
    }

    public Dictionary<string, string> HomeBatteryPowerInversionHeaders()
    {
        return GetBaseConfiguration().HomeBatteryPowerInversionHeaders;
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

    public string? HomeBatterySocJsonPattern()
    {
        return GetBaseConfiguration().HomeBatterySocJsonPattern;
    }

    public string? HomeBatterySocXmlPattern()
    {
        return GetBaseConfiguration().HomeBatterySocXmlPattern;
    }

    public string? HomeBatterySocXmlAttributeHeaderName()
    {
        return GetBaseConfiguration().HomeBatterySocXmlAttributeHeaderName;
    }

    public string? HomeBatterySocXmlAttributeHeaderValue()
    {
        return GetBaseConfiguration().HomeBatterySocXmlAttributeHeaderValue;
    }

    public string? HomeBatterySocXmlAttributeValueName()
    {
        return GetBaseConfiguration().HomeBatterySocXmlAttributeValueName;
    }

    public string? HomeBatteryPowerJsonPattern()
    {
        return GetBaseConfiguration().HomeBatteryPowerJsonPattern;
    }

    public string? HomeBatteryPowerXmlPattern()
    {
        return GetBaseConfiguration().HomeBatteryPowerXmlPattern;
    }

    public string? HomeBatteryPowerXmlAttributeHeaderName()
    {
        return GetBaseConfiguration().HomeBatteryPowerXmlAttributeHeaderName;
    }

    public string? HomeBatteryPowerXmlAttributeHeaderValue()
    {
        return GetBaseConfiguration().HomeBatteryPowerXmlAttributeHeaderValue;
    }

    public string? HomeBatteryPowerXmlAttributeValueName()
    {
        return GetBaseConfiguration().HomeBatteryPowerXmlAttributeValueName;
    }

    public decimal CurrentPowerToGridCorrectionFactor()
    {
        return GetBaseConfiguration().CurrentPowerToGridCorrectionFactor;
    }

    public decimal CurrentInverterPowerCorrectionFactor()
    {
        return GetBaseConfiguration().CurrentInverterPowerCorrectionFactor;
    }

    public decimal HomeBatterySocCorrectionFactor()
    {
        return GetBaseConfiguration().HomeBatterySocCorrectionFactor;
    }

    public decimal HomeBatteryPowerCorrectionFactor()
    {
        return GetBaseConfiguration().HomeBatteryPowerCorrectionFactor;
    }

    public int? HomeBatteryMinSoc()
    {
        return GetBaseConfiguration().HomeBatteryMinSoc;
    }

    public int? HomeBatteryChargingPower()
    {
        return GetBaseConfiguration().HomeBatteryChargingPower;
    }

    public string TeslaMateApiBaseUrl()
    {
        return GetBaseConfiguration().TeslaMateApiBaseUrl;
    }

    /// <summary>
    /// Get max combined current from baseConfiguration
    /// </summary>
    /// <returns>Configured max combined current. If no value is configured int.MaxValue is returned.</returns>
    public int MaxCombinedCurrent()
    {
        return GetBaseConfiguration().MaxCombinedCurrent ?? int.MaxValue;
    }

    public string GeoFence()
    {
        return GetBaseConfiguration().GeoFence;
    }

    public TimeSpan TimespanUntilSwitchOn()
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

    public int PowerBuffer(bool getInMemoryValueIfAvailable)
    {
        if (getInMemoryValueIfAvailable)
        {
            var settingsPowerBuffer = _settings.PowerBuffer;
            if (settingsPowerBuffer != null)
            {
                return settingsPowerBuffer.Value;
            }
        }
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
    
    public FrontendConfiguration? FrontendConfiguration()
    {
        return GetBaseConfiguration().FrontendConfiguration;
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
        _logger.LogTrace("Config value extracted: [{key}]: {value}", environmentVariableName, value);
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
        var jsonFileContent = await BaseConfigurationJsonFileContent().ConfigureAwait(false);

        var dtoBaseConfiguration = JsonConvert.DeserializeObject<DtoBaseConfiguration>(jsonFileContent)!;

        if (dtoBaseConfiguration == null)
        {
            throw new ArgumentException($"Could not deserialize {jsonFileContent} to {nameof(DtoBaseConfiguration)}");
        }

        if (dtoBaseConfiguration.FrontendConfiguration == null)
        {
            CreateDefaultFrontendConfiguration(dtoBaseConfiguration);
        }


        return dtoBaseConfiguration;
    }

    internal void CreateDefaultFrontendConfiguration(DtoBaseConfiguration dtoBaseConfiguration)
    {
        dtoBaseConfiguration.FrontendConfiguration = new FrontendConfiguration();

        SetHomeBatteryDefaultConfiguration(dtoBaseConfiguration);

        SetGridDefaultFrontendConfiguration(dtoBaseConfiguration);

        SetInverterDefaultFrontendConfiguration(dtoBaseConfiguration);
    }

    private void SetInverterDefaultFrontendConfiguration(DtoBaseConfiguration dtoBaseConfiguration)
    {
        dtoBaseConfiguration.FrontendConfiguration ??= new FrontendConfiguration();

        if (!string.IsNullOrEmpty(dtoBaseConfiguration.CurrentInverterPowerMqttTopic))
        {
            dtoBaseConfiguration.FrontendConfiguration.InverterValueSource = SolarValueSource.Mqtt;
        }
        else if (dtoBaseConfiguration.IsModbusCurrentInverterPowerUrl)
        {
            dtoBaseConfiguration.FrontendConfiguration.InverterValueSource = SolarValueSource.Modbus;
        }
        else if (!string.IsNullOrEmpty(dtoBaseConfiguration.CurrentInverterPowerUrl))
        {
            dtoBaseConfiguration.FrontendConfiguration.InverterValueSource = SolarValueSource.Rest;
        }
        else
        {
            dtoBaseConfiguration.FrontendConfiguration.InverterValueSource = SolarValueSource.None;
        }

        dtoBaseConfiguration.FrontendConfiguration.InverterPowerNodePatternType =
            _nodePatternTypeHelper.DecideNodePatternType(dtoBaseConfiguration.CurrentInverterPowerJsonPattern,
                dtoBaseConfiguration.CurrentInverterPowerXmlPattern);
    }

    private void SetGridDefaultFrontendConfiguration(DtoBaseConfiguration dtoBaseConfiguration)
    {
        dtoBaseConfiguration.FrontendConfiguration ??= new FrontendConfiguration();
        if (!string.IsNullOrEmpty(dtoBaseConfiguration.CurrentPowerToGridMqttTopic))
        {
            dtoBaseConfiguration.FrontendConfiguration.GridValueSource = SolarValueSource.Mqtt;
        }
        else if (dtoBaseConfiguration.IsModbusGridUrl)
        {
            dtoBaseConfiguration.FrontendConfiguration.GridValueSource = SolarValueSource.Modbus;
        }
        else if (!string.IsNullOrEmpty(dtoBaseConfiguration.CurrentPowerToGridUrl))
        {
            dtoBaseConfiguration.FrontendConfiguration.GridValueSource = SolarValueSource.Rest;
        }
        else
        {
            dtoBaseConfiguration.FrontendConfiguration.GridValueSource = SolarValueSource.None;
        }

        dtoBaseConfiguration.FrontendConfiguration.GridPowerNodePatternType =
            _nodePatternTypeHelper.DecideNodePatternType(dtoBaseConfiguration.CurrentPowerToGridJsonPattern,
                dtoBaseConfiguration.CurrentPowerToGridXmlPattern);
    }

    private void SetHomeBatteryDefaultConfiguration(DtoBaseConfiguration dtoBaseConfiguration)
    {
        dtoBaseConfiguration.FrontendConfiguration ??= new FrontendConfiguration();
        if (string.IsNullOrEmpty(dtoBaseConfiguration.HomeBatteryPowerMqttTopic)
            && string.IsNullOrEmpty(dtoBaseConfiguration.HomeBatteryPowerUrl)
            && string.IsNullOrEmpty(dtoBaseConfiguration.HomeBatterySocMqttTopic)
            && string.IsNullOrEmpty(dtoBaseConfiguration.HomeBatterySocUrl)
           )
        {
            dtoBaseConfiguration.FrontendConfiguration.HomeBatteryValuesSource = SolarValueSource.None;
        }
        else if (!string.IsNullOrEmpty(dtoBaseConfiguration.HomeBatteryPowerMqttTopic)
                 || !string.IsNullOrEmpty(dtoBaseConfiguration.HomeBatterySocMqttTopic))
        {
            dtoBaseConfiguration.FrontendConfiguration.HomeBatteryValuesSource = SolarValueSource.Mqtt;
        }
        else if (dtoBaseConfiguration.IsModbusHomeBatteryPowerUrl
                 || dtoBaseConfiguration.IsModbusHomeBatterySocUrl)
        {
            dtoBaseConfiguration.FrontendConfiguration.HomeBatteryValuesSource = SolarValueSource.Modbus;
        }
        else
        {
            dtoBaseConfiguration.FrontendConfiguration.HomeBatteryValuesSource = SolarValueSource.Rest;
        }

        dtoBaseConfiguration.FrontendConfiguration.HomeBatteryPowerNodePatternType =
            _nodePatternTypeHelper.DecideNodePatternType(dtoBaseConfiguration.HomeBatteryPowerJsonPattern,
                dtoBaseConfiguration.HomeBatteryPowerXmlPattern);

        dtoBaseConfiguration.FrontendConfiguration.HomeBatterySocNodePatternType =
            _nodePatternTypeHelper.DecideNodePatternType(dtoBaseConfiguration.HomeBatterySocJsonPattern,
                dtoBaseConfiguration.HomeBatterySocXmlPattern);
    }

    public bool ShouldIgnoreSslErrors()
    {
        _logger.LogTrace("{method}()", nameof(ShouldIgnoreSslErrors));
        var environmentVariableName = "IgnoreSslErrors";
        var value = _configuration.GetValue<bool>(environmentVariableName);
        return value;
    }

    public async Task TryAutoFillUrls()
    {
        var dtoBaseConfiguration = await GetBaseConfigurationAsync().ConfigureAwait(false);
        if (!string.IsNullOrEmpty(dtoBaseConfiguration.CurrentPowerToGridUrl) && !string.IsNullOrEmpty(dtoBaseConfiguration.CurrentPowerToGridMqttTopic))
        {
            return;
        }
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMilliseconds(500);
        //ToDo: as the plugin has to use the host network the pluginname is unknown
        //try
        //{
        //    var result = await httpClient.GetAsync("http://smaplugin:7192/api/Hello/IsAlive");
        //    if (result.IsSuccessStatusCode)
        //    {
        //        dtoBaseConfiguration.CurrentPowerToGridUrl = "http://smaplugin:7192/api/CurrentPower/GetPower";
        //        return;
        //    }
        //}
        //catch (Exception ex)
        //{
        //    _logger.LogWarning(ex, "Could not load values from SMA Plugin");
        //}

        try
        {
            //ToDo: update url in Frontend
            //var result = await httpClient.GetAsync("http://solaredgeplugin/api/Hello/IsAlive").ConfigureAwait(false);
            //if (result.IsSuccessStatusCode)
            //{
            //    dtoBaseConfiguration.CurrentPowerToGridUrl = "http://solaredgeplugin/CurrentValues/GetPowerToGrid";
            //    return;
            //}
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load values from SolarEdge Plugin");
        }

        try
        {
            //ToDo: update url in Frontend
            //var result = await httpClient.GetAsync("http://modbusplugin/api/Hello/IsAlive").ConfigureAwait(false);
            //if (result.IsSuccessStatusCode)
            //{
            //    dtoBaseConfiguration.IsModbusGridUrl = true;
            //    dtoBaseConfiguration.CurrentPowerToGridUrl = "http://modbusplugin/api/Modbus/GetInt32Value?unitIdentifier=3&startingAddress=&quantity=&ipAddress=&port=502&factor=1&connectDelaySeconds=1&timeoutSeconds=10";
            //}
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load values from Modbus Plugin");
        }
    }

    private async Task<string> BaseConfigurationJsonFileContent()
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

        return jsonFileContent ?? throw new InvalidOperationException("Could not read BaseConfigurationJson file content.");
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
        if (File.Exists(configFileLocation))
        {
            try
            {
                File.Copy(configFileLocation, configFileLocation + _dateTimeProvider.DateTimeOffSetNow().ToUnixTimeSeconds(), true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not backup baseConfig.json");
            }
        }
        await File.WriteAllTextAsync(configFileLocation, jsonFileContent).ConfigureAwait(false);
        var cache = MemoryCache.Default;
        cache.Remove(_baseConfigurationMemoryCacheName, CacheEntryRemovedReason.ChangeMonitorChanged);
    }

    public async Task<bool> IsBaseConfigurationJsonRelevant()
    {
        return await Task.FromResult(File.Exists(BaseConfigFileFullName())).ConfigureAwait(false);
    }

    public async Task UpdateBaseConfigurationAsync(DtoBaseConfiguration dtoBaseConfiguration)
    {
        var baseConfigurationBase = (BaseConfigurationBase)dtoBaseConfiguration;
        var baseConfigurationJson = JsonConvert.DeserializeObject<BaseConfigurationJson>(JsonConvert.SerializeObject(baseConfigurationBase));

        if (baseConfigurationJson == null)
        {
            throw new InvalidOperationException("Could not deserialize dtoBaseConfiguration to baseconfigurationJson");
        }
        baseConfigurationJson.LastEditDateTime = _dateTimeProvider.UtcNow();

        var baseConfigurationJsonString = JsonConvert.SerializeObject(baseConfigurationJson);

        await UpdateJsonFile(BaseConfigFileFullName(), baseConfigurationJsonString).ConfigureAwait(false);
    }

    public bool ShouldDisplayApiRequestCounter()
    {
        return _configuration.GetValue<bool>("DisplayApiRequestCounter");
    }
}
