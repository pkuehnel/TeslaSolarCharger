namespace TeslaSolarCharger.Server.Dtos;

public class DtoLoggedError
{
    public int Id { get; set; }
    public List<DateTime> Occurrences { get; set; } = new();
    public string? Vin { get; set; }
    public string Message { get; set; }
}
