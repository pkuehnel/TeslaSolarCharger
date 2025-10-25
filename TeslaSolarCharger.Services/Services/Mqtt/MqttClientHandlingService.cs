using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Text;
using TeslaSolarCharger.Services.Services.Mqtt.Contracts;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Services.Services.ValueRefresh;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.MqttConfiguration;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Services.Services.Mqtt;

public class MqttClientHandlingService(ILogger<MqttClientHandlingService> logger,
    IServiceProvider serviceProvider,
    IRestValueExecutionService restValueExecutionService,
    IDateTimeProvider dateTimeProvider,
    MqttClientFactory mqttClientFactory,
    IConstants constants)
    : IMqttClientHandlingService
{
    private readonly Dictionary<string, IMqttClient> _mqttClients = new();
    private readonly ConcurrentDictionary<string, AutoRefreshingValue<decimal>> _values = new();

    public IReadOnlyDictionary<ValueUsage, List<DtoHistoricValue<decimal>>> GetSolarValues()
    {
        logger.LogTrace("{method}()", nameof(GetSolarValues));
        var result = new Dictionary<ValueUsage, List<DtoHistoricValue<decimal>>>();
        var valueUsages = new HashSet<ValueUsage>
        {
            ValueUsage.InverterPower,
            ValueUsage.GridPower,
            ValueUsage.HomeBatteryPower,
            ValueUsage.HomeBatterySoc,
        };
        foreach (var mqttKeyValues in _values.Values)
        {
            foreach (var (valueKey, resultValues) in mqttKeyValues.HistoricValues)
            {
                if (valueKey.ValueUsage == default || !valueUsages.Contains(valueKey.ValueUsage.Value))
                {
                    continue;
                }
                if (!result.ContainsKey(valueKey.ValueUsage.Value))
                {
                    result[valueKey.ValueUsage.Value] = new();
                }
                result[valueKey.ValueUsage.Value].AddRange(resultValues.Values);
            }
        }
        return result;
    }

    public ReadOnlyDictionary<string, AutoRefreshingValue<decimal>> GetRawValues()
    {
        logger.LogTrace("{method}()", nameof(GetRawValues));
        return new(_values);
    }

    public async Task ConnectClient(DtoMqttConfiguration mqttConfiguration, List<DtoMqttResultConfiguration> resultConfigurations, bool forceReconnection)
    {
        var key = CreateMqttClientKey(mqttConfiguration.Host, mqttConfiguration.Port, mqttConfiguration.Username);
        if (!forceReconnection && _mqttClients.TryGetValue(key, out var client))
        {
            if (client.IsConnected)
            {
                return;
            }
            await ConnectClient(mqttConfiguration, resultConfigurations, true);
            return;
        }
        RemoveClientByKey(key);
        var guid = Guid.NewGuid();
        var mqqtClientId = $"TeslaSolarCharger{guid}";
        var mqttClientOptions = new MqttClientOptionsBuilder()
            .WithClientId(mqqtClientId)
            .WithTimeout(TimeSpan.FromSeconds(5))
            .WithTcpServer(mqttConfiguration.Host, mqttConfiguration.Port)
            //Required as iobroker does not support newer versions, see https://tff-forum.de/t/teslasolarcharger-pv-ueberschussladen-mit-beliebiger-wallbox-teil-2/350867/1697?u=mane123
            .WithProtocolVersion(MqttProtocolVersion.V311)
            .Build();

        if (!string.IsNullOrWhiteSpace(mqttConfiguration.Username) && !string.IsNullOrEmpty(mqttConfiguration.Password))
        {
            logger.LogTrace("Add username and password to mqtt client options");
            var utf8 = Encoding.UTF8;
            var passwordBytes = utf8.GetBytes(mqttConfiguration.Password);
            mqttClientOptions.Credentials = new MqttClientCredentials(mqttConfiguration.Username, passwordBytes);
        }
        var mqttClient = serviceProvider.GetRequiredService<IMqttClient>();
        mqttClient.ApplicationMessageReceivedAsync += e =>
        {
            var topicResultConfigurations = resultConfigurations
                .Where(x => x.Topic == e.ApplicationMessage.Topic)
                .ToList();
            if (topicResultConfigurations.Count < 1)
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
            foreach (var resultConfiguration in topicResultConfigurations)
            {
                var value = restValueExecutionService.GetValue(payloadString, resultConfiguration.NodePatternType, resultConfiguration);
                var mqttKeyValues = _values.GetOrAdd(key, new AutoRefreshingValue<decimal>(constants.SolarHistoricValueCapacity));
                var valueKey = new ValueKey(mqttConfiguration.Id, ConfigurationType.MqttSolarValue, resultConfiguration.UsedFor, null);
                mqttKeyValues.UpdateValue(valueKey, dateTimeProvider.DateTimeOffSetUtcNow(), value, resultConfiguration.Id);
            }
            return Task.CompletedTask;
        };
        await mqttClient.ConnectAsync(mqttClientOptions);
        
        var mqttSubscribeOptions = mqttClientFactory.CreateSubscribeOptionsBuilder()
            .Build();

        if (resultConfigurations.Count > 0)
        {
            mqttSubscribeOptions.TopicFilters = GetMqttTopicFilters(resultConfigurations);
            await mqttClient.SubscribeAsync(mqttSubscribeOptions).ConfigureAwait(false);
        }
        _mqttClients.Add(key, mqttClient);
    }

    private List<MqttTopicFilter> GetMqttTopicFilters(List<DtoMqttResultConfiguration> resultConfigurations)
    {
        var topicFilters = new List<MqttTopicFilter>();
        foreach (var resultConfiguration in resultConfigurations)
        {
            if (topicFilters.Any(f => string.Equals(f.Topic, resultConfiguration.Topic)))
            {
                continue;
            }
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
        return string.IsNullOrEmpty(userName) ? $"{host}:{port}" : $"{host}:{port};{userName}";
    }

    public IMqttClient? GetClientByKey(string key)
    {
        if (_mqttClients.TryGetValue(key, out var client))
        {
            return client;
        }
        return default;
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

        _values.TryRemove(key, out _);
    }
}
