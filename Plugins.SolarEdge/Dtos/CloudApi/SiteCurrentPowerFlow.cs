#pragma warning disable CS8618
namespace Plugins.SolarEdge.Dtos.CloudApi;

public class SiteCurrentPowerFlow
{
    public int UpdateRefreshRate { get; set; }
    public string Unit { get; set; }
    public List<Connection> Connections { get; set; }
    public Grid Grid { get; set; }
    public Load Load { get; set; }
    public Pv Pv { get; set; }
    public Storage Storage { get; set; }
}
