using Microsoft.Extensions.Logging;
using TeslaSolarCharger.Services.Services.Mqtt.Contracts;

namespace TeslaSolarCharger.Services.Services.Mqtt;

public class MqttClientReconnectionService(ILogger<MqttClientReconnectionService> logger,
    IMqttConfigurationService mqttConfigurationService,
    IMqttClientHandlingService mqttClientHandlingService) : IMqttClientReconnectionService
{
    public async Task ReconnectMqttClients()
    {
        var mqttConfigurations = await mqttConfigurationService.GetMqttConfigurationsByPredicate(x => true);
        foreach (var dtoMqttConfiguration in mqttConfigurations)
        {
            var clientKey = mqttClientHandlingService.CreateMqttClientKey(dtoMqttConfiguration.Host, dtoMqttConfiguration.Port, dtoMqttConfiguration.Username);
            var client = mqttClientHandlingService.GetClientByKey(clientKey);
            if (client == null || !client.IsConnected)
            {
                var resultConfigurations = await mqttConfigurationService.GetMqttResultConfigurationsByPredicate(x => x.MqttConfigurationId == dtoMqttConfiguration.Id);
                try
                {
                    await mqttClientHandlingService.ConnectClient(dtoMqttConfiguration, resultConfigurations, true);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while reconnecting MqttClient with key {key}", clientKey);
                }
            }
        }
    }
}
