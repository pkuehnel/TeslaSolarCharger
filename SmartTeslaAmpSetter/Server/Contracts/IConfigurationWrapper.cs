namespace SmartTeslaAmpSetter.Server.Contracts;

public interface IConfigurationWrapper
{
    string ConfigFileLocation();
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
}