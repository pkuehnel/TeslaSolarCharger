using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

public class FrontendConfiguration
{
    public SolarValueSource GridValueSource { get; set; }
    public SolarValueSource HomeBatteryValueSource { get; set; }
    public SolarValueSource InverterValueSource { get; set; }

    public NodePatternType? GridPowerNodePatternType { get; set; }
    public NodePatternType? HomeBatteryPowerNodePatternType { get; set; }
    public NodePatternType? HomeBatterySocNodePatternType { get; set; }
    public NodePatternType? InverterPowerNodePatternType { get; set; }
}
