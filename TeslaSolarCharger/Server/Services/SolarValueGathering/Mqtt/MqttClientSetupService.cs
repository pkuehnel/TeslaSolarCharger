using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using System.Linq.Expressions;
using System.Text;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Mqtt.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Rest.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.MqttConfiguration;
using TeslaSolarCharger.Shared.Resources;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Mqtt;

public class MqttClientSetupService : IAutoRefreshingValueSetupService, IMqttClientSetupService
{
    private readonly ILogger<MqttClientSetupService> _logger;
    private readonly IMqttConfigurationService _mqttConfigurationService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IConstants _constants;

    public MqttClientSetupService(ILogger<MqttClientSetupService> logger,
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
        _logger.LogTrace("{method}({@configurationIds})", nameof(GetDecimalAutoRefreshingValuesAsync), configurationIds);
        Expression<Func<MqttConfiguration, bool>> predicate = configurationIds.Count == 0 ? x => true : x => configurationIds.Contains(x.Id);
        var mqttConfigurations = await _mqttConfigurationService.GetMqttConfigurationsByPredicate(predicate);
        _logger.LogTrace("Found {count} MQTT configurations", mqttConfigurations.Count);
        var result = new List<IAutoRefreshingValue<decimal>>();
        foreach (var dtoMqttConfiguration in mqttConfigurations)
        {
            _logger.LogTrace("Get MQTT result configurations for MQTT configuration with ID {id}", dtoMqttConfiguration.Id);
            var resultConfigurations = await _mqttConfigurationService.GetMqttResultConfigurationsByPredicate(x => x.MqttConfigurationId == dtoMqttConfiguration.Id);
            _logger.LogTrace("Found {count} result configurations", resultConfigurations.Count);
            var value = CreateMqttAutoValueAsync(dtoMqttConfiguration, resultConfigurations);
            result.Add(value);
        }

        return result;
    }

    private IAutoRefreshingValue<decimal> CreateMqttAutoValueAsync(
    DtoMqttConfiguration mqttConfiguration,
    List<DtoMqttResultConfiguration> resultConfigurations)
    {
        _logger.LogTrace("{method}({@mqttConfiguration}, {@resultConfigurations})", nameof(CreateMqttAutoValueAsync), mqttConfiguration, resultConfigurations);
        var sourceKey = new SourceValueKey(mqttConfiguration.Id, ConfigurationType.MqttSolarValue);

        var autoValue = new AutoRefreshingValue<decimal>(
            _serviceScopeFactory,
            async (sp, self, ct) =>
            {
                // everything MQTT-related lives here:
                var logger = sp.GetRequiredService<ILogger<MqttClientSetupService>>();
                var dateTimeProvider = sp.GetRequiredService<IDateTimeProvider>();
                var restValueExecutionService = sp.GetRequiredService<IRestValueExecutionService>();
                var mqttClientFactory = sp.GetRequiredService<MqttClientFactory>();
                var configurationWrapper = sp.GetRequiredService<IConfigurationWrapper>();

                var client = sp.GetRequiredService<IMqttClient>();
                var mqttClientId = GenerateClientId(configurationWrapper.MqttClientIdPrefix());

                var optionsBuilder = new MqttClientOptionsBuilder()
                    .WithClientId(mqttClientId)
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

                Func<MqttClientDisconnectedEventArgs, Task> disconnectHandler = e =>
                {
                    if (!ct.IsCancellationRequested)
                    {
                        logger.LogWarning(e.Exception, "MQTT client disconnected from {host}:{port}. Reason: {reason}", mqttConfiguration.Host, mqttConfiguration.Port, e.Reason);
                    }
                    return Task.CompletedTask;
                };

                // Subscribe handler
                client.ApplicationMessageReceivedAsync += handler;

                client.DisconnectedAsync += disconnectHandler;

                try
                {
                    const int retryIntervalSeconds = 30;
                    var isSubscribed = false;
                    while (!ct.IsCancellationRequested)
                    {
                        try
                        {
                            if (!client.IsConnected)
                            {
                                logger.LogTrace("Connecting MQTT client to {host}:{port}", mqttConfiguration.Host, mqttConfiguration.Port);
                                await client.ConnectAsync(options, ct).ConfigureAwait(false);
                                logger.LogTrace("MQTT client connected to {host}:{port}", mqttConfiguration.Host, mqttConfiguration.Port);
                                isSubscribed = false;
                            }

                            if (client.IsConnected && !isSubscribed && resultConfigurations.Count > 0)
                            {
                                var subscribeOptions = mqttClientFactory.CreateSubscribeOptionsBuilder().Build();
                                subscribeOptions.TopicFilters = GetMqttTopicFilters(resultConfigurations);

                                logger.LogTrace("Subscribing to {count} topics", subscribeOptions.TopicFilters.Count);
                                await client.SubscribeAsync(subscribeOptions, ct).ConfigureAwait(false);
                                logger.LogTrace("Successfully subscribed to {count} topics", subscribeOptions.TopicFilters.Count);
                                isSubscribed = true;
                            }
                        }
                        catch (Exception ex) when (!ct.IsCancellationRequested)
                        {
                            // Catch connection/subscription errors so they don't crash the loop
                            logger.LogError(ex, "Error connecting or subscribing to MQTT broker at {host}:{port}. Will retry in {retryIntervalSeconds}s.", mqttConfiguration.Host, mqttConfiguration.Port, retryIntervalSeconds);
                        }

                        // Delay before checking the connection state again (prevents CPU spamming)
                        await Task.Delay(TimeSpan.FromSeconds(retryIntervalSeconds), ct).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    logger.LogTrace("MQTT connection to {host}:{port} cancelled via token", mqttConfiguration.Host, mqttConfiguration.Port);
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Fatal error in MQTT client loop for {host}:{port}", mqttConfiguration.Host, mqttConfiguration.Port);
                }
                finally
                {
                    // cleanup: detach handler & dispose client
                    client.ApplicationMessageReceivedAsync -= handler;
                    client.DisconnectedAsync -= disconnectHandler;

                    try
                    {
                        if (client.IsConnected)
                        {
                            logger.LogTrace("Disconnecting MQTT client from {host}:{port}", mqttConfiguration.Host, mqttConfiguration.Port);
                            // ReSharper disable once MethodSupportsCancellation
                            await client.DisconnectAsync().ConfigureAwait(false);
                            logger.LogTrace("MQTT client disconnected from {host}:{port}", mqttConfiguration.Host, mqttConfiguration.Port);
                        }
                        else
                        {
                            logger.LogTrace("MQTT client already disconnected from {host}:{port}", mqttConfiguration.Host, mqttConfiguration.Port);
                        }
                    }
                    catch(Exception ex)
                    {
                        logger.LogError(ex, "Error while disconnecting MQTT client from {host}:{port}", mqttConfiguration.Host, mqttConfiguration.Port);
                    }

                    client.Dispose();
                }
            },
            _constants.SolarHistoricValueCapacity,
            sourceKey);

        return autoValue;
    }

    public string GenerateClientId(string prefix)
    {
        //Limit length as MQTT spec allows only 23 characters for client id, enforce random part
        if (prefix.Length >= StaticConstants.MaxMqttPrefixLength)
        {
            prefix = prefix.Substring(0, StaticConstants.MaxMqttPrefixLength);
        }
        const int mqttClientIdLength = 23;
        var shortGuid = Guid.NewGuid().ToString().Substring(0, mqttClientIdLength - prefix.Length);
        var mqttClientId = $"{prefix}{shortGuid}";
        return mqttClientId;
    }

    private List<MqttTopicFilter> GetMqttTopicFilters(List<DtoMqttResultConfiguration> resultConfigurations)
    {
        _logger.LogTrace("{method}({@resultConfigurations})", nameof(GetMqttTopicFilters), resultConfigurations);
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
