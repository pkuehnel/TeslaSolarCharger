namespace SmartTeslaAmpSetter.Server.Contracts;

public interface IMqttService
{
    Task ConfigureMqttClient();
}