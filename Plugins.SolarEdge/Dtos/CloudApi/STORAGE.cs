#pragma warning disable CS8618
namespace Plugins.SolarEdge.Dtos.CloudApi;

public class Storage
{
    public string Status { get; set; }
    public double CurrentPower { get; set; }
    public int ChargeLevel { get; set; }
    public bool Critical { get; set; }
}
