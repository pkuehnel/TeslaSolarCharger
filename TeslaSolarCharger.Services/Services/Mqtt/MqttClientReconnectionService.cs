using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using System.Linq.Expressions;
using System.Text;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Services.Services.Mqtt.Contracts;
using TeslaSolarCharger.Services.Services.Rest.Contracts;
using TeslaSolarCharger.Services.Services.ValueRefresh;
using TeslaSolarCharger.Services.Services.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.MqttConfiguration;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Services.Services.Mqtt;

public class MqttClientReconnectionService : IAutoRefreshingValueSetupService
{
    private readonly ILogger<MqttClientReconnectionService> _logger;
    private readonly IMqttConfigurationService _mqttConfigurationService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IConstants _constants;

    public MqttClientReconnectionService(ILogger<MqttClientReconnectionService> logger,
        IMqttConfigurationService mqttConfigurationService,
        IServiceScopeFactory serviceScopeFactory,
        IConstants constants)
    {
        _logger = logger;
        _mqttConfigurationService = mqttConfigurationService;
        _serviceScopeFactory = serviceScopeFactory;
        _constants = constants;
    }

    public ConfigurationType ConfigurationType => ConfigurationType.MqttSolarValue;

    public async Task<List<IAutoRefreshingValue<decimal>>> GetDecimalAutoRefreshingValuesAsync(List<int> configurationIds)
    {
        _logger.LogTrace("{method}({@configurationIds}", nameof(GetDecimalAutoRefreshingValuesAsync), configurationIds);
        Expression<Func<MqttConfiguration, bool>> predicate = configurationIds.Count == 0 ? x => true : x => configurationIds.Contains(x.Id);
        var mqttConfigurations = await _mqttConfigurationService.GetMqttConfigurationsByPredicate(predicate);

        var result = new List<IAutoRefreshingValue<decimal>>();
        foreach (var dtoMqttConfiguration in mqttConfigurations)
        {
            var resultConfigurations = await _mqttConfigurationService.GetMqttResultConfigurationsByPredicate(x => x.MqttConfigurationId == dtoMqttConfiguration.Id);
            var value = CreateMqttAutoValueAsync(dtoMqttConfiguration, resultConfigurations);
            result.Add(value);
        }

        return result;
    }

    private IAutoRefreshingValue<decimal> CreateMqttAutoValueAsync(
    DtoMqttConfiguration mqttConfiguration,
    List<DtoMqttResultConfiguration> resultConfigurations)
    {
        var sourceKey = new SourceValueKey(mqttConfiguration.Id, ConfigurationType.MqttSolarValue);

        var autoValue = new AutoRefreshingValue<decimal>(
            _serviceScopeFactory,
            async (sp, self, ct) =>
            {
                // everything MQTT-related lives here:
                var logger = sp.GetRequiredService<ILogger<AutoRefreshingValueHandlingService>>();
                var dateTimeProvider = sp.GetRequiredService<IDateTimeProvider>();
                var restValueExecutionService = sp.GetRequiredService<IRestValueExecutionService>();
                var mqttClientFactory = sp.GetRequiredService<MqttClientFactory>();

                var client = sp.GetRequiredService<IMqttClient>();
                var guid = Guid.NewGuid();
                var mqqtClientId = $"TeslaSolarCharger{guid}";

                var optionsBuilder = new MqttClientOptionsBuilder()
                    .WithClientId(mqqtClientId)
                    .WithTimeout(TimeSpan.FromSeconds(5))
                    .WithTcpServer(mqttConfiguration.Host, mqttConfiguration.Port)
                    .WithProtocolVersion(MqttProtocolVersion.V311);

                if (!string.IsNullOrWhiteSpace(mqttConfiguration.Username) &&
                    !string.IsNullOrEmpty(mqttConfiguration.Password))
                {
                    logger.LogTrace("Add username and password to mqtt client options");
                    var utf8 = Encoding.UTF8;
                    var passwordBytes = utf8.GetBytes(mqttConfiguration.Password);
                    optionsBuilder.WithCredentials(mqttConfiguration.Username, passwordBytes);
                }

                var options = optionsBuilder.Build();

                Func<MqttApplicationMessageReceivedEventArgs, Task> handler = async e =>
                {
                    if (ct.IsCancellationRequested)
                        return;

                    var topicResultConfigurations = resultConfigurations
                        .Where(x => x.Topic == e.ApplicationMessage.Topic)
                        .ToList();

                    if (topicResultConfigurations.Count < 1)
                    {
                        logger.LogDebug("No result configuration found for topic {topic}", e.ApplicationMessage.Topic);
                        return;
                    }

                    var payloadString = e.ApplicationMessage.ConvertPayloadToString();
                    if (payloadString == default)
                    {
                        logger.LogWarning("Received empty payloadString for topic {topic}", e.ApplicationMessage.Topic);
                        return;
                    }

                    logger.LogDebug("Received value {payloadString} for topic {topic}", payloadString, e.ApplicationMessage.Topic);

                    var now = dateTimeProvider.DateTimeOffSetUtcNow();

                    foreach (var resultConfiguration in topicResultConfigurations)
                    {
                        var value = restValueExecutionService.GetValue(
                            payloadString,
                            resultConfiguration.NodePatternType,
                            resultConfiguration);

                        var valueKey = new ValueKey(resultConfiguration.UsedFor, null, resultConfiguration.Id);
                        self.UpdateValue(valueKey, now, value);
                    }

                    await Task.CompletedTask;
                };

                // Subscribe handler
                client.ApplicationMessageReceivedAsync += handler;

                try
                {
                    await client.ConnectAsync(options, ct).ConfigureAwait(false);

                    if (resultConfigurations.Count > 0)
                    {
                        var subscribeOptions = mqttClientFactory.CreateSubscribeOptionsBuilder().Build();
                        subscribeOptions.TopicFilters = GetMqttTopicFilters(resultConfigurations);
                        await client.SubscribeAsync(subscribeOptions, ct).ConfigureAwait(false);
                    }

                    // Stay alive until cancellation
                    var tcs = new TaskCompletionSource<object?>();
                    await using (ct.Register(() => tcs.TrySetResult(null)))
                    {
                        await tcs.Task.ConfigureAwait(false);
                    }
                }
                finally
                {
                    // cleanup: detach handler & dispose client
                    client.ApplicationMessageReceivedAsync -= handler;

                    try
                    {
                        if (client.IsConnected)
                            await client.DisconnectAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored
                    }

                    client.Dispose();
                }
            },
            _constants.SolarHistoricValueCapacity,
            sourceKey);

        return autoValue;
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
}
