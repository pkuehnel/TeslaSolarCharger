namespace TeslaSolarCharger.Server.Contracts;

public interface ISolarMqttService
{
    Task ConnectMqttClient();
    Task ConnectClientIfNotConnected();
    Task DisconnectClient(string reason);
}
