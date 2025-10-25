using Microsoft.Extensions.Logging;
using TeslaSolarCharger.Services.Services.Mqtt.Contracts;
using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

namespace TeslaSolarCharger.Services.Services.Mqtt;

public class MqttExecutionService(ILogger<MqttExecutionService> logger,
    IMqttClientHandlingService mqttClientHandlingService,
    IMqttConfigurationService mqttConfigurationService) : IMqttExecutionService
{
    public async Task<List<DtoValueConfigurationOverview>> GetMqttValueOverviews()
    {
        logger.LogTrace("{method}()", nameof(GetMqttValueOverviews));
        var overviews = new List<DtoValueConfigurationOverview>();
        var mqttResults = mqttClientHandlingService.GetRawValues();
        var mqttConfigurations = await mqttConfigurationService.GetMqttConfigurationsByPredicate(x => true);
        foreach (var mqttConfiguration in mqttConfigurations)
        {
            var clientKey = mqttClientHandlingService.CreateMqttClientKey(mqttConfiguration.Host, mqttConfiguration.Port, mqttConfiguration.Username);
            var valueOverview = new DtoValueConfigurationOverview()
            {
                Heading = clientKey,
                Id = mqttConfiguration.Id,
            };
            var resultConfigurations =
                await mqttConfigurationService.GetMqttResultConfigurationsByPredicate(x => x.MqttConfigurationId == mqttConfiguration.Id);
            foreach (var resultConfiguration in resultConfigurations)
            {
                decimal? value = null;
                if (mqttResults.TryGetValue(clientKey, out var result))
                {
                    foreach (var historicValue in result.HistoricValues)
                    {
                        foreach (var dtoHistoricValue in historicValue.Value)
                        {
                            if (value == default)
                            {
                                value = dtoHistoricValue.Value.Value;
                            }
                            else
                            {
                                value += dtoHistoricValue.Value.Value;
                            }
                        }
                    }
                }
                else
                {
                    value = null;
                }
                valueOverview.Results.Add(new DtoOverviewValueResult
                {
                    Id = resultConfiguration.Id,
                    UsedFor = resultConfiguration.UsedFor,
                    CalculatedValue = value,
                });
            }
            overviews.Add(valueOverview);
        }
        return overviews;
    }
}
