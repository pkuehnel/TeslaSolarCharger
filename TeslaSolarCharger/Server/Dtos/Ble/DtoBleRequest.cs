namespace TeslaSolarCharger.Server.Dtos.Ble;

public class DtoBleRequest
{
    public string Vin { get; set; }
    public string CommandName { get; set; }
    public string? Domain { get; set; }
    public List<string> Parameters { get; set; } = new();

    /// <summary>
    /// Only set on scheduled polls: keeps the worker of the used adapter warm for that many seconds. One-off commands
    /// leave it null so they never change the container's stored warm window.
    /// </summary>
    public int? KeepWarmSeconds { get; set; }
}
