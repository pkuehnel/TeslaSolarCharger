namespace TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

public class DtoDynamicMinSocSettings
{
    public bool? DynamicHomeBatteryMinSoc { get; set; }
    public int? HomeBatteryMinSoc { get; set; }
    public int? HomeBatteryMinDynamicMinSoc { get; set; }
    public int? HomeBatteryMaxDynamicMinSoc { get; set; }
}
