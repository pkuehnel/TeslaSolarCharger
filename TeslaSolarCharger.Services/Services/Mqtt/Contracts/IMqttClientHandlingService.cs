using TeslaSolarCharger.Shared.Dtos.BaseConfiguration;
using TeslaSolarCharger.Shared.Dtos.MqttConfiguration;

namespace TeslaSolarCharger.Services.Services.Mqtt.Contracts;

public interface IMqttClientHandlingService
{
    Task ConnectClient(DtoMqttConfiguration mqttConfiguration, List<DtoMqttResultConfiguration> resultConfigurations);
    void RemoveClient(string host, int port, string? userName);
    List<DtoValueConfigurationOverview> GetMqttValueOverviews();
    List<DtoMqttResult> GetMqttValues();
}
