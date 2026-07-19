using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Support;

public class DtoDebugCar
{
    public string? Vin { get; set; }
    public string? Name { get; set; }
    public bool ShouldBeManaged { get; set; }
    public bool IsAvailableInTeslaAccount { get; set; }
    public CarType CarType { get; set; }
    public bool UseBle { get; set; }
    public bool? IsOnline { get; set; }
}
