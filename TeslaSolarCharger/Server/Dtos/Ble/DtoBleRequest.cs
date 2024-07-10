namespace TeslaSolarCharger.Server.Dtos.Ble;

public class DtoBleRequest
{
    public string Vin { get; set; }
    public string CommandName { get; set; }
    public string Domain { get; set; } = string.Empty;
    public List<string> Parameters { get; set; } = new();
}
