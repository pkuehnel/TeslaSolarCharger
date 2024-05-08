namespace TeslaSolarCharger.Shared.Dtos.MqttConfiguration;

public class DtoMqttConfiguration
{
    public int Id { get; set; }
    public string Host { get; set; }
    public int Port { get; set; } = 1883;
    public string? Username { get; set; }
    public string? Password { get; set; }
}
