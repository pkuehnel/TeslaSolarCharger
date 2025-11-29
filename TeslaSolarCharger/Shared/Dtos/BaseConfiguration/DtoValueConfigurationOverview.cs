namespace TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

public class DtoValueConfigurationOverview
{
    public DtoValueConfigurationOverview(string heading)
    {
        Heading = heading;
    }

    public int Id { get; set; }
    public string Heading { get; set; }
    public List<DtoOverviewValueResult> Results { get; set; } = new();
}
