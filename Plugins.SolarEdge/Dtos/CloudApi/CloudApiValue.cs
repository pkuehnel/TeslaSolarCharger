namespace Plugins.SolarEdge.Dtos.CloudApi;

// CloudApiValue myDeserializedClass = JsonConvert.DeserializeObject<CloudApiValue>(myJsonResponse);

public class CloudApiValue
{
    public SiteCurrentPowerFlow SiteCurrentPowerFlow { get; set; }
}