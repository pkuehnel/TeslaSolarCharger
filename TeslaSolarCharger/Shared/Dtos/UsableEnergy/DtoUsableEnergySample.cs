using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.UsableEnergy;

public class DtoUsableEnergySample
{
    public UsableEnergySampleKind Kind { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public int StartSoc { get; set; }
    public int EndSoc { get; set; }
    /// <summary>Energy as metered; for cars the energy taken from the wall. Null when it was not measured.</summary>
    public double? EnergyKwh { get; set; }
    /// <summary>Usable energy this sample implies, null when the sample could not be measured.</summary>
    public double? ImpliedUsableEnergyKwh { get; set; }
    public UsableEnergySampleRejectionReason? RejectionReason { get; set; }
    /// <summary>Whether the sample is one of the newest accepted ones the learned value is calculated from.</summary>
    public bool IsUsedForEstimate { get; set; }
}
