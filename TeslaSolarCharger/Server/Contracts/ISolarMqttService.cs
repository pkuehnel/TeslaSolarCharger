namespace TeslaSolarCharger.Server.Contracts;

public interface ISolarMqttService
{
    Task ConnectMqttClient();
}
