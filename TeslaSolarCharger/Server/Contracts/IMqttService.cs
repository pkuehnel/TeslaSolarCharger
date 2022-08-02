namespace TeslaSolarCharger.Server.Contracts;

public interface IMqttService
{
    Task ConnectMqttClient();
}