namespace TeslaSolarCharger.Shared.Dtos.Support;

public class DtoDebugCar
{
    public string? Vin { get; set; }
    public string? Name { get; set; }
    public bool ShouldBeManaged { get; set; }
    public bool IsAvailableInTeslaAccount { get; set; }
}
