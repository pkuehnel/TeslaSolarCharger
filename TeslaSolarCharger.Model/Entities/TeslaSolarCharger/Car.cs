using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class Car
{
    public int Id { get; set; }
    public int TeslaMateCarId { get; set; }
    public string? Name { get; set; }
    public string? Vin { get; set; }
    public TeslaCarFleetApiState TeslaFleetApiState { get; set; } = TeslaCarFleetApiState.NotConfigured;
    public ChargeMode ChargeMode { get; set; }
    public int MinimumSoc { get; set; }
    public DateTime LatestTimeToReachSoC { get; set; }

    public bool IgnoreLatestTimeToReachSocDate { get; set; }

    public int MaximumAmpere { get; set; }

    public int MinimumAmpere { get; set; }

    public int UsableEnergy { get; set; }

    public bool? ShouldBeManaged { get; set; }
    public bool? ShouldSetChargeStartTimes { get; set; }

    public int ChargingPriority { get; set; }
    
}
