namespace SolarTeslaCharger.Server.Contracts;

public interface IMqttService
{
    Task ConfigureMqttClient();
}