namespace Plugins.SolarEdge.Dtos.CloudApi;

public class SiteCurrentPowerFlow
{
    public int updateRefreshRate { get; set; }
    public string unit { get; set; }
    public List<Connection> connections { get; set; }
    public GRID GRID { get; set; }
    public LOAD LOAD { get; set; }
    public PV PV { get; set; }
    public STORAGE STORAGE { get; set; }
}