namespace TeslaSolarCharger.Services.Services.Mqtt.Contracts;

public interface IMqttClientReconnectionService
{
    Task ReconnectMqttClients();
}
