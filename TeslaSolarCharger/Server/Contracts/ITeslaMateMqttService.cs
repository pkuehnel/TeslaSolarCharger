namespace TeslaSolarCharger.Server.Contracts;

public interface ITeslaMateMqttService
{
    Task ConnectMqttClient();
    bool IsMqttClientConnected { get; }
}
