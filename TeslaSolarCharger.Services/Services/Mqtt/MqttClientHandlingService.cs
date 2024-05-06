using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using MQTTnet.Server;
using MudBlazor;
using System.Text;
using TeslaSolarCharger.Services.Services.Mqtt.Contracts;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;
using TeslaSolarCharger.Shared.Dtos.MqttConfiguration;

namespace TeslaSolarCharger.Services.Services.Mqtt;

public class MqttClientHandlingService(ILogger<MqttClientHandlingService> logger,
    IServiceProvider serviceProvider,
    IRestValueExecutionService restValueExecutionService,
    IDateTimeProvider dateTimeProvider,
    MqttFactory mqttFactory)
    : IMqttClientHandlingService
{
    private readonly Dictionary<string, IMqttClient> _mqttClients = new();
    private readonly Dictionary<int, DtoMqttResult> _mqttResults = new();

    public List<DtoMqttResult> GetMqttValues()
    {
        logger.LogTrace("{method}()", nameof(GetMqttValues));
        return _mqttResults.Values.ToList();
    }

    public Dictionary<int, DtoMqttResult> GetMqttValueDictionary()
    {
        return _mqttResults.ToDictionary(x => x.Key, x => x.Value);
    }

    public async Task ConnectClient(DtoMqttConfiguration mqttConfiguration, List<DtoMqttResultConfiguration> resultConfigurations)
    {
        var key = CreateMqttClientKey(mqttConfiguration.Host, mqttConfiguration.Port, mqttConfiguration.Username);
        RemoveClientByKey(key);
        var guid = Guid.NewGuid();
        var mqqtClientId = $"TeslaSolarCharger{guid}";
        var mqttClientOptions = new MqttClientOptionsBuilder()
            .WithClientId(mqqtClientId)
            .WithTimeout(TimeSpan.FromSeconds(5))
            .WithTcpServer(mqttConfiguration.Host, mqttConfiguration.Port)
            .Build();

        if (!string.IsNullOrWhiteSpace(mqttConfiguration.Username) && !string.IsNullOrEmpty(mqttConfiguration.Password))
        {
            var utf8 = Encoding.UTF8;
            var passwordBytes = utf8.GetBytes(mqttConfiguration.Password);
            mqttClientOptions.Credentials = new MqttClientCredentials(mqttConfiguration.Username, passwordBytes);
        }
        var mqttClient = serviceProvider.GetRequiredService<IMqttClient>();
        mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            var resultConfiguration = resultConfigurations.FirstOrDefault(x => x.Topic == e.ApplicationMessage.Topic);
            if (resultConfiguration == default)
            {
                logger.LogDebug("No result configuration found for topic {topic}", e.ApplicationMessage.Topic);
                return Task.CompletedTask;
            }
            var payloadString = e.ApplicationMessage.ConvertPayloadToString();
            if (payloadString == default)
            {
                logger.LogWarning("Received empty payloadString for topic {topic}", e.ApplicationMessage.Topic);
                return Task.CompletedTask;
            }
            logger.LogDebug("Received value {payloadString} for topic {topic}", payloadString, e.ApplicationMessage.Topic);
            var value = restValueExecutionService.GetValue(payloadString, resultConfiguration.NodePatternType, resultConfiguration);
            var mqttResult = new DtoMqttResult
            {
                Value = value,
                UsedFor = resultConfiguration.UsedFor,
                TimeStamp = dateTimeProvider.DateTimeOffSetUtcNow(),
                Key = key,
            };
            _mqttResults[resultConfiguration.Id] = mqttResult;
            return Task.CompletedTask;
        };
        await mqttClient.ConnectAsync(mqttClientOptions);
        _mqttClients.Add(key, mqttClient);

        var mqttSubscribeOptions = mqttFactory.CreateSubscribeOptionsBuilder()
            .Build();

        mqttSubscribeOptions.TopicFilters = GetMqttTopicFilters(resultConfigurations);

        await mqttClient.SubscribeAsync(mqttSubscribeOptions).ConfigureAwait(false);
    }

    private List<MqttTopicFilter> GetMqttTopicFilters(List<DtoMqttResultConfiguration> resultConfigurations)
    {
        var topicFilters = new List<MqttTopicFilter>();
        foreach (var resultConfiguration in resultConfigurations)
        {
            var topicFilter = new MqttTopicFilter
            {
                Topic = resultConfiguration.Topic,
                QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce,
            };
            topicFilters.Add(topicFilter);
        }
        return topicFilters;
    }

    public void RemoveClient(string host, int port, string? userName)
    {
        logger.LogTrace("{method}({host}, {port}, {userName})", nameof(RemoveClient), host, port, userName);
        var key = CreateMqttClientKey(host, port, userName);
        RemoveClientByKey(key);
    }

    public string CreateMqttClientKey(string host, int port, string? userName)
    {
        return $"{host}:{port};{userName}";
    }

    private void RemoveClientByKey(string key)
    {
        if (_mqttClients.TryGetValue(key, out var client))
        {
            if (client.IsConnected)
            {
                client.DisconnectAsync();
            }
            client.Dispose();
            _mqttClients.Remove(key);
        }

        var resultIds = _mqttResults.Where(r => r.Value.Key == key).Select(r => r.Key);
        foreach (var resultId in resultIds)
        {
            _mqttResults.Remove(resultId);
        }
    }
}
