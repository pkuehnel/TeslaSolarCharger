using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Server;
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
    IDateTimeProvider dateTimeProvider)
    : IMqttClientHandlingService
{
    private readonly Dictionary<string, IMqttClient> _mqttClients = new();
    private readonly Dictionary<int, DtoMqttResult> _mqttResults = new();

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
    }

    public List<DtoValueConfigurationOverview> GetMqttValueOverviews()
    {
        logger.LogTrace("{method}()", nameof(GetMqttValueOverviews));
        var overviews = new List<DtoValueConfigurationOverview>();
        foreach (var mqttClient in _mqttClients)
        {
            var valueOverview = new DtoValueConfigurationOverview() { Heading = mqttClient.Key, };
            foreach (var mqttResult in _mqttResults)
            {
                if (mqttResult.Value.Key == mqttClient.Key)
                {
                    valueOverview.Results.Add(new DtoOverviewValueResult
                    {
                        Id = mqttResult.Key,
                        UsedFor = mqttResult.Value.UsedFor,
                        CalculatedValue = mqttResult.Value.Value,
                    });
                }
            }
            overviews.Add(valueOverview);
        }
        return overviews;
    }

    public void RemoveClient(string host, int port, string? userName)
    {
        logger.LogTrace("{method}({host}, {port}, {userName})", nameof(RemoveClient), host, port, userName);
        var key = CreateMqttClientKey(host, port, userName);
        RemoveClientByKey(key);
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

    private string CreateMqttClientKey(string host, int port, string? userName)
    {
        return $"{host}:{port};{userName}";
    }
}
