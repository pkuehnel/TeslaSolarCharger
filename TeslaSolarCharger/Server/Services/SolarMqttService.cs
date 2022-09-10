using MQTTnet.Client;
using MQTTnet;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class SolarMqttService : ISolarMqttService
{
    private readonly ILogger<SolarMqttService> _logger;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly IMqttClient _mqttClient;
    private readonly MqttFactory _mqttFactory;
    private readonly ISettings _setting;
    private readonly IPvValueService _pvValueService;

    public SolarMqttService(ILogger<SolarMqttService> logger, IConfigurationWrapper configurationWrapper,
        IMqttClient mqttClient, MqttFactory mqttFactory, ISettings setting, IPvValueService pvValueService)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
        _mqttClient = mqttClient;
        _mqttFactory = mqttFactory;
        _setting = setting;
        _pvValueService = pvValueService;
    }

    public async Task ConnectMqttClient()
    {
        _logger.LogTrace("{method}()", nameof(ConnectMqttClient));
        //ToDo: Client Id dynmaisch machen
        var mqqtClientId = "TeslaSolarCharger";
        var mqttServer = GetMqttServerAndPort(out var mqttServerPort);
        var mqttClientOptions = new MqttClientOptionsBuilder()
            .WithClientId(mqqtClientId)
            .WithTcpServer(mqttServer, mqttServerPort)
            .Build();

        if (string.IsNullOrWhiteSpace(mqttServer))
        {
            return;
        }

        _mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            var value = e.ApplicationMessage.ConvertPayloadToString();
            _logger.LogTrace("Payload for topic {topic} is {value}", e.ApplicationMessage.Topic, value);
            var gridJsonPattern = _configurationWrapper.CurrentPowerToGridJsonPattern();
            var gridXmlPattern = _configurationWrapper.CurrentPowerToGridXmlPattern();
            var gridCorrectionFactor = (double)_configurationWrapper.CurrentPowerToGridCorrectionFactor();
            _setting.Overage = _pvValueService.GetIntegerValueByString(value, gridJsonPattern, gridXmlPattern, gridCorrectionFactor);
            if (_setting.Overage != null)
            {
                _pvValueService.AddOverageValueToInMemoryList((int)_setting.Overage);
            }
            return Task.CompletedTask;
        };

        try
        {
            if (_mqttClient.IsConnected)
            {
                await _mqttClient.DisconnectAsync(MqttClientDisconnectReason.AdministrativeAction,
                    "Reconnecting with new configuration").ConfigureAwait(false);
            }
            await _mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not connect to Solar mqtt server");
            return;
        }

        var mqttSubscribeOptions = _mqttFactory.CreateSubscribeOptionsBuilder()
            .WithTopicFilter(f =>
            {
                f.WithTopic($"{_configurationWrapper.CurrentPowerToGridMqttTopic()}");
            })
            .Build();

        await _mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None).ConfigureAwait(false);
    }

    internal string? GetMqttServerAndPort(out int? mqttServerPort)
    {
        var mqttServerIncludingPort = _configurationWrapper.SolarMqttServer();
        var mqttServerAndPort = mqttServerIncludingPort?.Split(":");
        var mqttServer = mqttServerAndPort?.FirstOrDefault();
        mqttServerPort = null;
        if (mqttServerAndPort != null && mqttServerAndPort.Length > 1)
        {
            mqttServerPort = Convert.ToInt32(mqttServerAndPort[1]);
        }

        return mqttServer;
    }
}
