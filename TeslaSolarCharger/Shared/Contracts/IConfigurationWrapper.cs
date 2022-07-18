using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

namespace TeslaSolarCharger.Shared.Contracts;

public interface IConfigurationWrapper
{
    string CarConfigFileFullName();
    TimeSpan ChargingValueJobUpdateIntervall();
    TimeSpan PvValueJobUpdateIntervall();
    string MqqtClientId();
    string MosquitoServer();
    string CurrentPowerToGridUrl();
    string? CurrentInverterPowerUrl();
    string? CurrentPowerToGridJsonPattern();
    bool CurrentPowerToGridInvertValue();
    string TeslaMateApiBaseUrl();
    List<int> CarPriorities();
    string GeoFence();
    TimeSpan TimeUntilSwitchOn();
    TimeSpan TimespanUntilSwitchOff();
    int PowerBuffer();
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
}