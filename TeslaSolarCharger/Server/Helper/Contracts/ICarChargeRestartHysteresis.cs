namespace TeslaSolarCharger.Server.Helper.Contracts;

/// <summary>
/// A car that is not charging does not start charging again while its SoC is close to its charge limit.
/// This is the single source of truth for that hysteresis, used for charge start and for charging targets.
/// </summary>
public interface ICarChargeRestartHysteresis
{
    /// <summary>
    /// A car that is not charging only starts charging when its SoC is at least this many percent below the limit.
    /// </summary>
    int Percent { get; }

    /// <summary>
    /// True if a car that is not charging will not start charging towards <paramref name="socLimit"/> at <paramref name="soc"/>.
    /// </summary>
    bool PreventsChargeStart(int? soc, int? socLimit);
}
