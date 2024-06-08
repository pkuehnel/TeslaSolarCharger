namespace TeslaSolarCharger.Server.Dtos.Ble;

public class DtoBleRequest
{
    public string Vin { get; set; }
    public string CommandName { get; set; }
    public List<string> Parameters { get; set; } = new();
}
