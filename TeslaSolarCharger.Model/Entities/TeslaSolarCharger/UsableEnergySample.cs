using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

/// <summary>
/// One measurement of a battery's usable energy: the energy that moved between two SoC readings. Rejected
/// measurements are kept with their reason so the UI can explain why nothing is learned yet.
/// </summary>
public class UsableEnergySample
{
    public int Id { get; set; }
    public UsableEnergySampleKind Kind { get; set; }

    public int? CarId { get; set; }
    public Car? Car { get; set; }

    /// <summary>
    /// The car's VIN when the sample was taken. A different VIN means a different vehicle behind the same car entry,
    /// so the sample no longer describes that car's battery.
    /// </summary>
    public string? CarVin { get; set; }

    /// <summary>The charging process a car sample was taken from, so each charge is only measured once.</summary>
    public int? ChargingProcessId { get; set; }

    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public int StartSoc { get; set; }
    public int EndSoc { get; set; }

    /// <summary>
    /// Net energy that moved in (charge) or out (discharge) of the battery between the two SoC readings, as metered.
    /// For cars this is the energy taken from the wall, the charge loss is removed when the sample is evaluated so a
    /// changed charge loss setting also applies to old samples.
    /// </summary>
    public int EnergyWh { get; set; }

    public UsableEnergySampleRejectionReason? RejectionReason { get; set; }
}
