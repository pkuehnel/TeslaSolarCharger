using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class DepricatedCarConfiguration
{
    public DepricatedCarConfiguration()
    {
        ShouldBeManaged = true;
    }

    public ChargeMode ChargeMode { get; set; }

    public int MinimumSoC { get; set; }

    public DateTime LatestTimeToReachSoC { get; set; }

    public bool IgnoreLatestTimeToReachSocDate { get; set; }

    public int MaximumAmpere { get; set; }

    public int MinimumAmpere { get; set; }

    public int UsableEnergy { get; set; }

    public bool? ShouldBeManaged { get; set; }
    public bool? ShouldSetChargeStartTimes { get; set; }

    public int ChargingPriority { get; set; }
}
