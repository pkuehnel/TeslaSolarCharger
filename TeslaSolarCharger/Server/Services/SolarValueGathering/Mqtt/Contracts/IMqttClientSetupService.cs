namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Mqtt.Contracts;

public interface IMqttClientSetupService
{
    string GenerateClientId(string prefix);
}
