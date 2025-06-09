using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Home;

public class DtoChargingConnectorOverview
{
    public DtoChargingConnectorOverview(string name)
    {
        Name = name;
    }

    public string Name { get; set; }
    public bool IsCharging { get; set; }
    public bool IsPluggedIn { get; set; }
    public ChargeModeV2 ChargeMode { get; set; }
}
