namespace TeslaSolarCharger.Shared.Dtos.Settings;

/// <summary>
/// Result of a dynamic home battery SoC target calculation. Contains all data required to later
/// schedule hold/charge slots based on grid prices.
/// </summary>
public class DtoHomeBatterySocTarget
{
    /// <summary>
    /// The SoC in percent the battery would need right now (including the buffer) so it neither breaches the minimum
    /// SoC nor misses the target SoC until <see cref="SelfSufficiencyTime"/>. Not limited by the battery, so it exceeds
    /// 100 % when the deficit is larger than the battery.
    /// </summary>
    public int NeedSocPercent { get; set; }

    /// <summary>
    /// The highest SoC that is useful right now: limited by the battery capacity, by solar surplus that would otherwise
    /// overflow the battery before <see cref="SelfSufficiencyTime"/> and by the configured maximum dynamic min SoC.
    /// Only applies where a SoC is set or charged to, never to holding.
    /// </summary>
    public int MaxUsefulSocPercent { get; set; }

    /// <summary>
    /// <see cref="NeedSocPercent"/> limited to <see cref="MaxUsefulSocPercent"/>.
    /// </summary>
    public int CappedSocPercent => Math.Min(NeedSocPercent, MaxUsefulSocPercent);

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
