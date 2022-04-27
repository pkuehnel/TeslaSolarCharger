namespace SmartTeslaAmpSetter.Server.Contracts;

public interface IConfigurationWrapper
{
    string ConfigFileLocation();
    TimeSpan UpdateIntervall();
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
}