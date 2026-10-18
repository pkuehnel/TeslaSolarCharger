using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.UsableEnergy;

/// <summary>What the accepted samples of one kind (car charges, home battery charges or discharges) add up to.</summary>
public class DtoUsableEnergyEstimate
{
    public UsableEnergySampleKind Kind { get; set; }
    public int AcceptedSampleCount { get; set; }
    public int RequiredSampleCount { get; set; }
    public double? MedianKwh { get; set; }
    /// <summary>Interquartile range of the accepted samples relative to their median.</summary>
    public double? SpreadPercent { get; set; }
    public double MaxSpreadPercent { get; set; }
    public bool IsReliable { get; set; }
}
