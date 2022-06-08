namespace Plugins.SolarEdge.Dtos.CloudApi;

// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);

public class Root
{
    public SiteCurrentPowerFlow siteCurrentPowerFlow { get; set; }
}