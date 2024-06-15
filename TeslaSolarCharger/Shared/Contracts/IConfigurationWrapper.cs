using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

namespace TeslaSolarCharger.Shared.Contracts;

public interface IConfigurationWrapper
{
    string CarConfigFileFullName();
    TimeSpan ChargingValueJobUpdateIntervall();
    TimeSpan PvValueJobUpdateIntervall();
    string MqqtClientId();
    string MosquitoServer();
    string? CurrentPowerToGridUrl();
    string? CurrentInverterPowerUrl();
    string? CurrentPowerToGridJsonPattern();
    decimal CurrentPowerToGridCorrectionFactor();
    string TeslaMateApiBaseUrl();
    string GeoFence();
    TimeSpan TimespanUntilSwitchOn();
    TimeSpan TimespanUntilSwitchOff();
    int PowerBuffer(bool getInMemoryValueIfAvailable);
    string? TelegramBotKey();
    string? TelegramChannelId();
    string? CurrentInverterPowerJsonPattern();
    string? CurrentPowerToGridXmlPattern();
    string? CurrentInverterPowerXmlPattern();
    string? CurrentPowerToGridXmlAttributeHeaderName();
    string? CurrentPowerToGridXmlAttributeHeaderValue();
    string? CurrentPowerToGridXmlAttributeValueName();
    string? CurrentInverterPowerXmlAttributeHeaderName();
    string? CurrentInverterPowerXmlAttributeHeaderValue();
    string? CurrentInverterPowerXmlAttributeValueName();
    string TeslaMateDbServer();
    int TeslaMateDbPort();
    string TeslaMateDbDatabaseName();
    string TeslaMateDbUser();
    string TeslaMateDbPassword();
    string BaseConfigFileFullName();

    Task<DtoBaseConfiguration> GetBaseConfigurationAsync();
    Task SaveBaseConfiguration(DtoBaseConfiguration baseConfiguration);
    Task<bool> IsBaseConfigurationJsonRelevant();
    Task UpdateBaseConfigurationAsync(DtoBaseConfiguration dtoBaseConfiguration);
    Dictionary<string, string> CurrentPowerToGridHeaders();
    Dictionary<string, string> CurrentInverterPowerHeaders();
    decimal CurrentInverterPowerCorrectionFactor();
    string? HomeBatterySocJsonPattern();
    string? HomeBatterySocXmlPattern();
    string? HomeBatterySocXmlAttributeHeaderName();
    string? HomeBatterySocXmlAttributeHeaderValue();
    string? HomeBatterySocXmlAttributeValueName();
    decimal HomeBatterySocCorrectionFactor();
    string? HomeBatterySocUrl();
    Dictionary<string, string> HomeBatterySocHeaders();
    string? HomeBatteryPowerUrl();
    Dictionary<string, string> HomeBatteryPowerHeaders();
    string? HomeBatteryPowerJsonPattern();
    string? HomeBatteryPowerXmlPattern();
    string? HomeBatteryPowerXmlAttributeHeaderName();
    string? HomeBatteryPowerXmlAttributeHeaderValue();
    string? HomeBatteryPowerXmlAttributeValueName();
    decimal HomeBatteryPowerCorrectionFactor();
    int? HomeBatteryMinSoc();
    int? HomeBatteryChargingPower();
    string SqliteFileFullName();
    string? SolarMqttServer();
    string? CurrentPowerToGridMqttTopic();
    string? HomeBatterySocMqttTopic();
    string? CurrentInverterPowerMqttTopic();
    string? HomeBatteryPowerMqttTopic();
    Task TryAutoFillUrls();
    string? SolarMqttUsername();
    string? SolarMqttPassword();
    string? HomeBatteryPowerInversionUrl();
    Dictionary<string, string> HomeBatteryPowerInversionHeaders();

    /// <summary>
    /// Get max combined current from baseConfiguration
    /// </summary>
    /// <returns>Configured max combined current. If no value is configured int.MaxValue is returned.</returns>
    int MaxCombinedCurrent();

    FrontendConfiguration? FrontendConfiguration();
    bool AllowCors();
    bool ShouldDisplayApiRequestCounter();
    bool ShouldIgnoreSslErrors();
    string BackupCopyDestinationDirectory();
    string GetSqliteFileNameWithoutPath();
    string BackupZipDirectory();
    string FleetApiClientId();
    bool UseFleetApi();
    string BackendApiBaseUrl();
    bool IsDevelopmentEnvironment();
    string GetAwattarBaseUrl();
    string? GetFleetApiBaseUrl();
    string RestoreTempDirectory();
    string ConfigFileDirectory();
    string AutoBackupsZipDirectory();
    bool LogLocationData();
    bool GetVehicleDataFromTesla();
    int? MaxInverterAcPower();
    string? BleBaseUrl();
}
