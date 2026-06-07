namespace TeslaSolarCharger.Shared.Dtos;

public class DtoSmartCarCompatibleVehicle
{
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public List<int> Years { get; set; } = new();
    public string? Region { get; set; }
    public string? PowertrainType { get; set; }
}
