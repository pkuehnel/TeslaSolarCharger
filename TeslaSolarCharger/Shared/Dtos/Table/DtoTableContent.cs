namespace TeslaSolarCharger.Shared.Dtos.Table;

public class DtoTableContent
{
    public DtoTableRow TableHeader { get; set; } = new();
    public List<DtoTableRow> TableData { get; set; } = new();
}
