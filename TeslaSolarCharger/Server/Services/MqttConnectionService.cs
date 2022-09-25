using TeslaSolarCharger.Server.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class MqttConnectionService : IMqttConnectionService
{
    private readonly ILogger<MqttConnectionService> _logger;
    private readonly ITeslaMateMqttService _teslaMateMqttService;
    private readonly ISolarMqttService _solarMqttService;

    public MqttConnectionService(ILogger<MqttConnectionService> logger,
        ITeslaMateMqttService teslaMateMqttService, ISolarMqttService solarMqttService)
    {
        _logger = logger;
        _teslaMateMqttService = teslaMateMqttService;
        _solarMqttService = solarMqttService;
    }

    public async Task ReconnectMqttServices()
    {
        _logger.LogTrace("{method}()", nameof(ReconnectMqttServices));
        try
        {
            await _teslaMateMqttService.ConnectClientIfNotConnected().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while connecting TeslaMateMqttService");
        }
        try
        {
            await _solarMqttService.ConnectClientIfNotConnected().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while connecting SolarMqttService");
        }
    }
}
