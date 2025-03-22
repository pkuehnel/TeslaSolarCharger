using MQTTnet;
using TeslaSolarCharger.Shared.Dtos.MqttConfiguration;

namespace TeslaSolarCharger.Services.Services.Mqtt.Contracts;

public interface IMqttClientHandlingService
{
    Task ConnectClient(DtoMqttConfiguration mqttConfiguration, List<DtoMqttResultConfiguration> resultConfigurations,
        bool forceReconnection);
    void RemoveClient(string host, int port, string? userName);
    List<DtoMqttResult> GetMqttValues();
    string CreateMqttClientKey(string host, int port, string? userName);
    Dictionary<int, DtoMqttResult> GetMqttValueDictionary();
    IMqttClient? GetClientByKey(string key);
}
