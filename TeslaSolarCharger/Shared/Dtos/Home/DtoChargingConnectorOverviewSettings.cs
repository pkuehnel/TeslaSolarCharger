using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Home;

public class DtoChargingConnectorOverviewSettings
{
    public DtoChargingConnectorOverviewSettings(string name)
    {
        Name = name;
    }

    public string Name { get; set; }
    public ChargeModeV2 ChargeMode { get; set; }
}


public class DtoChargingConnectorOverviewState
{
    public bool IsCharging { get; set; }
    public bool IsPluggedIn { get; set; }
    public bool IsOcppConnected { get; set; }
}
