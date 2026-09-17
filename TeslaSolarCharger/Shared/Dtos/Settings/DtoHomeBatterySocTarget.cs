namespace TeslaSolarCharger.Shared.Dtos.Settings;

/// <summary>
/// Result of a dynamic home battery SoC target calculation. Contains all data required to later
/// schedule hold/charge slots based on grid prices.
/// </summary>
public class DtoHomeBatterySocTarget
{
    /// <summary>
    /// The SoC in percent the battery needs right now so it neither breaches the minimum SoC nor misses the
    /// target SoC until <see cref="SelfSufficiencyTime"/>. Clamped to the configured maximum dynamic min SoC.
    /// </summary>
    public int RequiredInitialSocPercent { get; set; }

    /// <summary>
    /// Earliest time the battery would fall below the minimum SoC when starting at the minimum SoC floor.
    /// As the simulation starts at the floor this is the earliest possible breach time; with a higher actual
    /// SoC the breach happens later. Null when the minimum is never breached.
    /// </summary>
    public DateTimeOffset? FirstBreachTime { get; set; }

    /// <summary>
    /// Energy in Wh (including the configured buffer) that is missing when starting at the minimum SoC floor.
    /// Capped by the battery headroom before <see cref="SelfSufficiencyTime"/> but intentionally not reduced
    /// when <see cref="RequiredInitialSocPercent"/> is clamped, so the full deficit stays visible.
    /// </summary>
    public int AdditionalEnergyRequiredWh { get; set; }

    /// <summary>
    /// The time the calculation targets: the next sunrise adjusted to the first positive surplus, or the next
    /// sunset when the battery should be full by sunset. Hold/charge slots only make sense before this time.
    /// </summary>
    public DateTimeOffset SelfSufficiencyTime { get; set; }

    /// <summary>
    /// When this result was calculated, so consumers can detect stale data.
    /// </summary>
    public DateTimeOffset CalculatedAt { get; set; }
}
