namespace Plugins.SolarEdge.Dtos.CloudApi;

public class STORAGE
{
    public string status { get; set; }
    public double currentPower { get; set; }
    public int chargeLevel { get; set; }
    public bool critical { get; set; }
}