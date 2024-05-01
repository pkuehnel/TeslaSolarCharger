namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class MqttConfiguration
{
    public int Id { get; set; }
    public string Host { get; set; }
    public int Port { get; set; } = 1883;
    public string? Username { get; set; }
    public string? Password { get; set; }
}
