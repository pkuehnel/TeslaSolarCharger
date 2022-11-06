using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

public class FrontendConfiguration
{
    public SolarValueSource GridValueSource { get; set; }
    public SolarValueSource HomeBatteryValueSource { get; set; }
    public SolarValueSource InverterValueSource { get; set; }
}
