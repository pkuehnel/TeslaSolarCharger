namespace TeslaSolarCharger.Shared.Dtos.Table;

public class DtoTableRow
{
    public bool IsActive { get; set; }
    public List<string?> Elements { get; set; } = new();
}
