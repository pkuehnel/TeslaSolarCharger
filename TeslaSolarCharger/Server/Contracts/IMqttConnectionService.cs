namespace TeslaSolarCharger.Server.Contracts;

public interface IMqttConnectionService
{
    Task ReconnectMqttServices();
}
