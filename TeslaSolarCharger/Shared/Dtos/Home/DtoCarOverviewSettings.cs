using TeslaSolarCharger.Shared.Attributes;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Home;

public class DtoCarOverviewSettings
{
    public DtoCarOverviewSettings(string name)
    {
        Name = name;
    }

    public string Name { get; set; }
    [HelperText("Always charge at full speed until this soc even if there is not enough solar power")]
    [Postfix("%")]
    public int? MinSoc { get; set; }
    [HelperText("Stop charging at this soc even if there is enough solar power")]
    [Postfix("%")]
    public int? MaxSoc { get; set; }
    public ChargeModeV2 ChargeMode { get; set; }
}

public class DtoCarOverviewState
{
    public bool? IsOnline { get; set; }
    public int? Soc { get; set; }
    public int? CarSideSocLimit { get; set; }
    public bool IsCharging { get; set; }
    public bool IsHome { get; set; }
    public bool IsPluggedIn { get; set; }
    public bool? FleetTelemetryConnectedSinceAtLeastTenMinutes { get; set; }
}
