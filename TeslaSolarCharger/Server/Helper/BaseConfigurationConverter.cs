using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration.OldVersions.V0._1;

namespace TeslaSolarCharger.Server.Helper;

public class BaseConfigurationConverter : IBaseConfigurationConverter
{
    private readonly ILogger<BaseConfigurationConverter> _logger;
    private readonly IConfiguration _configuration;
    private readonly IConfigurationWrapper _configurationWrapper;

    public BaseConfigurationConverter(ILogger<BaseConfigurationConverter> logger, IConfiguration configuration,
        IConfigurationWrapper configurationWrapper)
    {
        _logger = logger;
        _configuration = configuration;
        _configurationWrapper = configurationWrapper;
    }

    public async Task ConvertAllEnvironmentVariables()
    {
        _logger.LogTrace("{method}()", nameof(ConvertAllEnvironmentVariables));
        if (await _configurationWrapper.IsBaseConfigurationJsonRelevant())
        {
            _logger.LogInformation("Do not convert environment variables to json file as json already exists.");
            return;
        }
        var dtoBaseConfiguration = new DtoBaseConfiguration()
        {
            CurrentPowerToGridUrl = _configuration.GetValue<string>("CurrentPowerToGridUrl"),
            CurrentInverterPowerUrl = _configuration.GetValue<string?>("CurrentInverterPowerUrl"),
            TeslaMateApiBaseUrl = _configuration.GetValue<string>("TeslaMateApiBaseUrl"),
            UpdateIntervalSeconds = _configuration.GetValue<int>("UpdateIntervalSeconds"),
            PvValueUpdateIntervalSeconds = _configuration.GetValue<int>("PvValueUpdateIntervalSeconds"),
            CarPriorities = _configuration.GetValue<string>("CarPriorities"),
            GeoFence = _configuration.GetValue<string>("GeoFence"),
            MinutesUntilSwitchOn = _configuration.GetValue<int>("MinutesUntilSwitchOn") == 0 ? 5 : _configuration.GetValue<int>("MinutesUntilSwitchOn"),
            MinutesUntilSwitchOff = _configuration.GetValue<int>("MinutesUntilSwitchOff") == 0 ? 5 : _configuration.GetValue<int>("MinutesUntilSwitchOff"),
            PowerBuffer = _configuration.GetValue<int>("PowerBuffer"),
            CurrentPowerToGridJsonPattern = _configuration.GetValue<string?>("CurrentPowerToGridJsonPattern"),
            CurrentPowerToGridCorrectionFactor = _configuration.GetValue<bool?>("CurrentPowerToGridInvertValue") != true ? -1 : 1,
            CurrentInverterPowerJsonPattern = _configuration.GetValue<string?>("CurrentInverterPowerJsonPattern"),
            TelegramBotKey = _configuration.GetValue<string?>("TelegramBotKey"),
            TelegramChannelId = _configuration.GetValue<string?>("TelegramChannelId"),
            TeslaMateDbServer = _configuration.GetValue<string>("TeslaMateDbServer"),
            TeslaMateDbPort = _configuration.GetValue<int>("TeslaMateDbPort"),
            TeslaMateDbDatabaseName = _configuration.GetValue<string>("TeslaMateDbDatabaseName"),
            TeslaMateDbUser = _configuration.GetValue<string>("TeslaMateDbUser"),
            TeslaMateDbPassword = _configuration.GetValue<string>("TeslaMateDbPassword"),
            MqqtClientId = _configuration.GetValue<string>("MqqtClientId"),
            MosquitoServer = _configuration.GetValue<string>("MosquitoServer"),
            CurrentPowerToGridXmlPattern = _configuration.GetValue<string?>("CurrentPowerToGridXmlPattern"),
            CurrentPowerToGridXmlAttributeHeaderName = _configuration.GetValue<string?>("CurrentPowerToGridXmlAttributeHeaderName"),
            CurrentPowerToGridXmlAttributeHeaderValue = _configuration.GetValue<string?>("CurrentPowerToGridXmlAttributeHeaderValue"),
            CurrentPowerToGridXmlAttributeValueName = _configuration.GetValue<string?>("CurrentPowerToGridXmlAttributeValueName"),
            CurrentInverterPowerXmlPattern = _configuration.GetValue<string?>("CurrentInverterPowerXmlPattern"),
            CurrentInverterPowerXmlAttributeHeaderName = _configuration.GetValue<string?>("CurrentInverterPowerAttributeHeaderName"),
            CurrentInverterPowerXmlAttributeHeaderValue = _configuration.GetValue<string?>("CurrentInverterPowerAttributeHeaderValue"),
            CurrentInverterPowerXmlAttributeValueName = _configuration.GetValue<string?>("CurrentInverterPowerAttributeValueName"),
        };

        await _configurationWrapper.SaveBaseConfiguration(dtoBaseConfiguration);
    }

    public async Task ConvertBaseConfigToCurrentVersion()
    {
        _logger.LogTrace("{method}()", nameof(ConvertBaseConfigToCurrentVersion));
        var oldBaseConfigurationJson = await File.ReadAllTextAsync(_configurationWrapper.BaseConfigFileFullName()).ConfigureAwait(false);
        var version = GetVersionFromBaseConfigurationJsonString(oldBaseConfigurationJson);
        oldBaseConfigurationJson = await File.ReadAllTextAsync(_configurationWrapper.BaseConfigFileFullName()).ConfigureAwait(false);
        await File.WriteAllTextAsync($"{_configurationWrapper.BaseConfigFileFullName()}.{version}", oldBaseConfigurationJson);
        if (version.Equals(new Version(0, 1)))
        {
            var oldBaseConfiguration =
                JsonConvert.DeserializeObject<BaseConfigurationJsonV0_1>(oldBaseConfigurationJson) ?? throw new InvalidOperationException("Could not deserialize baseConfigJson V0_1");
            ConvertV0_1ToV1_0(oldBaseConfiguration);
        }
    }

    internal Version GetVersionFromBaseConfigurationJsonString(string oldBaseConfigurationJson)
    {
        var token = JObject.Parse(oldBaseConfigurationJson).SelectToken("$.Version");
        if (token == null)
        {
            return new Version(0, 1);
        }
        var value = token.Value<string>();
        if (value == null)
        {
            return new Version(0, 1); ;
        }

        return Version.Parse(value);
    }

    internal BaseConfigurationJson ConvertV0_1ToV1_0(BaseConfigurationJsonV0_1 oldBaseConfiguration)
    {
        var baseConfiguration = new BaseConfigurationJson();
        oldBaseConfiguration.CopyProperties(baseConfiguration);
        baseConfiguration.Version = new Version(1, 0);
        baseConfiguration.CurrentPowerToGridCorrectionFactor = oldBaseConfiguration.CurrentPowerToGridInvertValue ? -1 : 1;
        baseConfiguration.LastEditDateTime = DateTime.UtcNow;
        UpdateGridPowerUrlIfNeeded(baseConfiguration);

        return baseConfiguration;
    }

    private void UpdateGridPowerUrlIfNeeded(BaseConfigurationJson baseConfiguration)
    {
        if (baseConfiguration.CurrentPowerToGridUrl == null)
        {
            return;
        }

        var uri = new Uri(baseConfiguration.CurrentPowerToGridUrl);
        try
        {
            var queryString = HttpUtility.ParseQueryString(uri.Query);
            var factorQueryKey = "factor";
            var factorString = queryString.Get(factorQueryKey);
            if (factorString == null)
            {
                return;
            }

            var factor = Convert.ToDecimal(factorString);
            queryString.Remove(factorQueryKey);
            baseConfiguration.CurrentPowerToGridCorrectionFactor *= factor;
            var pagePathWithoutQueryString = uri.GetLeftPart(UriPartial.Path);
            baseConfiguration.CurrentPowerToGridUrl = queryString.Count > 0
                ? $"{pagePathWithoutQueryString}?{queryString}"
                : pagePathWithoutQueryString;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not multiply modbusfactor with current factor.");
        }
    }
}