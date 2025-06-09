using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Home;

public class DtoCarOverview
{
    public DtoCarOverview(string name)
    {
        Name = name;
    }

    public string Name { get; set; }
    public int? Soc { get; set; }
    public int? CarSideSocLimit { get; set; }
    public int MinSoc { get; set; }
    public int MaxSoc { get; set; }
    public bool IsCharging { get; set; }
    public bool IsHome { get; set; }
    public bool IsPluggedIn { get; set; }
    public ChargeModeV2 ChargeMode { get; set; }
}
