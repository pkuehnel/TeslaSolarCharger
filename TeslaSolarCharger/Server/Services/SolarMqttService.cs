using MQTTnet.Client;
using MQTTnet;
using MQTTnet.Packets;
using System.Text;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Enums;

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
        var guid = Guid.NewGuid();
        var mqqtClientId = $"TeslaSolarCharger{guid}";
        var mqttServer = GetMqttServerAndPort(out var mqttServerPort);
        if (string.IsNullOrWhiteSpace(mqttServer))
        {
            _logger.LogDebug("No Mqtt Options defined for solar power. Do not connect MQTT Client.");
        }
        var mqttClientOptions = new MqttClientOptionsBuilder()
            .WithClientId(mqqtClientId)
            .WithTimeout(TimeSpan.FromSeconds(5))
            .WithTcpServer(mqttServer, mqttServerPort)
            .Build();

        if(!string.IsNullOrWhiteSpace(_configurationWrapper.SolarMqttUsername()) && !string.IsNullOrEmpty(_configurationWrapper.SolarMqttPassword()))
        {
            var utf8 = Encoding.UTF8;
            var password = utf8.GetBytes(_configurationWrapper.SolarMqttPassword()!);
            mqttClientOptions.Credentials = new MqttClientCredentials(_configurationWrapper.SolarMqttUsername(), password);
        }

        if (string.IsNullOrWhiteSpace(mqttServer))
        {
            return;
        }

        _mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            var value = e.ApplicationMessage.ConvertPayloadToString();
            var topic = e.ApplicationMessage.Topic;
            var frontendConfiguration = _configurationWrapper.FrontendConfiguration() ?? new FrontendConfiguration();
            _logger.LogTrace("Payload for topic {topic} is {value}", topic, value);
            if (topic == _configurationWrapper.CurrentPowerToGridMqttTopic() && frontendConfiguration.GridValueSource == SolarValueSource.Mqtt)
            {
                var patternType = frontendConfiguration.GridPowerNodePatternType ?? NodePatternType.Direct;
                var jsonPattern = _configurationWrapper.CurrentPowerToGridJsonPattern();
                var xmlPattern = _configurationWrapper.CurrentPowerToGridXmlPattern();
                var correctionFactor = (double)_configurationWrapper.CurrentPowerToGridCorrectionFactor();
                var xmlAttributeHeaderName = _configurationWrapper.CurrentPowerToGridXmlAttributeHeaderName();
                var xmlAttributeHeaderValue = _configurationWrapper.CurrentPowerToGridXmlAttributeHeaderValue();
                var xmlAttributeValueName = _configurationWrapper.CurrentPowerToGridXmlAttributeValueName();
                _setting.Overage = _pvValueService.GetIntegerValueByString(value, jsonPattern, xmlPattern, correctionFactor, patternType,
                    xmlAttributeHeaderName, xmlAttributeHeaderValue, xmlAttributeValueName);
                if (_setting.Overage != null)
                {
                    _pvValueService.AddOverageValueToInMemoryList((int)_setting.Overage);
                }
            }
            else if (topic == _configurationWrapper.CurrentInverterPowerMqttTopic() && frontendConfiguration.InverterValueSource == SolarValueSource.Mqtt)
            {
                var patternType = frontendConfiguration.InverterPowerNodePatternType ?? NodePatternType.Direct;
                var jsonPattern = _configurationWrapper.CurrentInverterPowerJsonPattern();
                var xmlPattern = _configurationWrapper.CurrentInverterPowerXmlPattern();
                var correctionFactor = (double)_configurationWrapper.CurrentInverterPowerCorrectionFactor();
                var xmlAttributeHeaderName = _configurationWrapper.CurrentInverterPowerXmlAttributeHeaderName();
                var xmlAttributeHeaderValue = _configurationWrapper.CurrentInverterPowerXmlAttributeHeaderValue();
                var xmlAttributeValueName = _configurationWrapper.CurrentInverterPowerXmlAttributeValueName();
                _setting.InverterPower = _pvValueService.GetIntegerValueByString(value, jsonPattern, xmlPattern, correctionFactor, patternType,
                xmlAttributeHeaderName, xmlAttributeHeaderValue, xmlAttributeValueName);
            }
            else if (topic == _configurationWrapper.HomeBatterySocMqttTopic() && frontendConfiguration.HomeBatteryValuesSource == SolarValueSource.Mqtt)
            {
                var patternType = frontendConfiguration.HomeBatterySocNodePatternType ?? NodePatternType.Direct;
                var jsonPattern = _configurationWrapper.HomeBatterySocJsonPattern();
                var xmlPattern = _configurationWrapper.HomeBatterySocXmlPattern();
                var correctionFactor = (double)_configurationWrapper.HomeBatterySocCorrectionFactor();
                var xmlAttributeHeaderName = _configurationWrapper.HomeBatterySocXmlAttributeHeaderName();
                var xmlAttributeHeaderValue = _configurationWrapper.HomeBatterySocXmlAttributeHeaderValue();
                var xmlAttributeValueName = _configurationWrapper.HomeBatterySocXmlAttributeValueName();
                _setting.HomeBatterySoc = _pvValueService.GetIntegerValueByString(value, jsonPattern, xmlPattern, correctionFactor, patternType,
                xmlAttributeHeaderName, xmlAttributeHeaderValue, xmlAttributeValueName);
            }
            else if (topic == _configurationWrapper.HomeBatteryPowerMqttTopic() && frontendConfiguration.HomeBatteryValuesSource == SolarValueSource.Mqtt)
            {
                var patternType = frontendConfiguration.HomeBatteryPowerNodePatternType ?? NodePatternType.Direct;
                var jsonPattern = _configurationWrapper.HomeBatteryPowerJsonPattern();
                var xmlPattern = _configurationWrapper.HomeBatteryPowerXmlPattern();
                var correctionFactor = (double)_configurationWrapper.HomeBatteryPowerCorrectionFactor();
                var xmlAttributeHeaderName = _configurationWrapper.HomeBatteryPowerXmlAttributeHeaderName();
                var xmlAttributeHeaderValue = _configurationWrapper.HomeBatteryPowerXmlAttributeHeaderValue();
                var xmlAttributeValueName = _configurationWrapper.HomeBatteryPowerXmlAttributeValueName();
                _setting.HomeBatteryPower = _pvValueService.GetIntegerValueByString(value, jsonPattern, xmlPattern, correctionFactor, patternType,
                xmlAttributeHeaderName, xmlAttributeHeaderValue, xmlAttributeValueName);
            }
            else
            {
                _logger.LogWarning("Received value does not match a topic");
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
            .Build();

        mqttSubscribeOptions.TopicFilters = GetMqttTopicFilters();

        await _mqttClient.SubscribeAsync(mqttSubscribeOptions, CancellationToken.None).ConfigureAwait(false);
    }

    private List<MqttTopicFilter> GetMqttTopicFilters()
    {
        var topicFilters = new List<MqttTopicFilter>();
        var topics = new List<string?>()
        {
            _configurationWrapper.CurrentPowerToGridMqttTopic(),
            _configurationWrapper.CurrentInverterPowerMqttTopic(),
            _configurationWrapper.HomeBatterySocMqttTopic(),
            _configurationWrapper.HomeBatteryPowerMqttTopic(),
        };
        foreach (var topic in topics)
        {
            if (!string.IsNullOrWhiteSpace(topic))
            {
                topicFilters.Add(GenerateMqttTopicFilter(topic));
            }
        }
        return topicFilters;
    }

    private MqttTopicFilter GenerateMqttTopicFilter(string topic)
    {
        var mqttTopicFilterBuilder = new MqttTopicFilterBuilder();
        mqttTopicFilterBuilder.WithTopic(topic);
        return mqttTopicFilterBuilder.Build();
    }

    public async Task ConnectClientIfNotConnected()
    {
        _logger.LogTrace("{method}()", nameof(ConnectClientIfNotConnected));
        if (_mqttClient.IsConnected)
        {
            _logger.LogTrace("MqttClient is connected");
            return;
        }
        _logger.LogInformation("SolarMqttClient is not connected. If you do note reveice your solar power values over MQTT this is nothing to worry about.");
        await ConnectMqttClient().ConfigureAwait(false);
    }

    internal string? GetMqttServerAndPort(out int? mqttServerPort)
    {
        var mqttServerIncludingPort = _configurationWrapper.SolarMqttServer();
        if (string.IsNullOrWhiteSpace(mqttServerIncludingPort))
        {
            _logger.LogDebug("No Solar MQTT Server defined, do not extract servername and port");
            mqttServerPort = null;
            return null;
        }
        var mqttServerAndPort = mqttServerIncludingPort.Split(":");
        var mqttServer = mqttServerAndPort.FirstOrDefault();
        mqttServerPort = null;
        if (mqttServerAndPort.Length > 1)
        {
            mqttServerPort = Convert.ToInt32(mqttServerAndPort[1]);
        }

        return mqttServer;
    }
}
