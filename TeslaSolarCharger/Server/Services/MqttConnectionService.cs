using TeslaSolarCharger.Server.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class MqttConnectionService(
    ILogger<MqttConnectionService> logger,
    ITeslaMateMqttService teslaMateMqttService,
    ISolarMqttService solarMqttService)
    : IMqttConnectionService
{
    public async Task ReconnectMqttServices()
    {
        logger.LogTrace("{method}()", nameof(ReconnectMqttServices));
        try
        {
            await teslaMateMqttService.ConnectClientIfNotConnected().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while connecting TeslaMateMqttService");
        }
        try
        {
            await solarMqttService.ConnectClientIfNotConnected().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while connecting SolarMqttService");
        }
    }
}
