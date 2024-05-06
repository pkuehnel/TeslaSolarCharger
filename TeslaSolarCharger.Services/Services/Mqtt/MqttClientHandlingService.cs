using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Server;
using System.Text;
using TeslaSolarCharger.Services.Services.Mqtt.Contracts;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Shared.Dtos.ModbusConfiguration;
using TeslaSolarCharger.Shared.Dtos.MqttConfiguration;

namespace TeslaSolarCharger.Services.Services.Mqtt;

public class MqttClientHandlingService(ILogger<MqttClientHandlingService> logger,
    IServiceProvider serviceProvider,
    IRestValueExecutionService restValueExecutionService,
    TimeProvider timeProvider)
    : IMqttClientHandlingService
{
    private readonly Dictionary<string, IMqttClient> _mqttClients = new();
    private readonly Dictionary<int, DtoMqttResult> _mqttResults = new();

    public void ConnectClient(DtoMqttConfiguration mqttConfiguration, List<DtoMqttResultConfiguration> resultConfigurations)
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
                TimeStamp = timeProvider.GetUtcNow(),
            };
            _mqttResults[resultConfiguration.Id] = mqttResult;
            return Task.CompletedTask;
        };
        _mqttClients.Add(key, mqttClient);
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
    }

    private string CreateMqttClientKey(string host, int port, string? userName)
    {
        return $"{host}:{port};{userName}";
    }
}
