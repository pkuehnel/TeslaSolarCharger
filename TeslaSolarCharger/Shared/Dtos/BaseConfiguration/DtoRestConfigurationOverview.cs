namespace TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

public class DtoRestConfigurationOverview
{
    public int Id { get; set; }
    public string Url { get; set; }
    public List<DtoRestValueResult> Results { get; set; }
}
