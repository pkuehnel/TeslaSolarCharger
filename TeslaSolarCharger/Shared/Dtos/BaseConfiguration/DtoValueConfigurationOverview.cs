namespace TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

public class DtoValueConfigurationOverview
{
    public int Id { get; set; }
    public string Heading { get; set; }
    public List<DtoOverviewValueResult> Results { get; set; } = new();
}
