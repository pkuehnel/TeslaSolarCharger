namespace TeslaSolarCharger.Server.Contracts;

public interface ITeslaMateMqttService
{
    bool IsMqttClientConnected { get; }
    Task ConnectClientIfNotConnected();
    Task DisconnectClient(string reason);
}
