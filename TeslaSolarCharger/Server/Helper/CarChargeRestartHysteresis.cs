namespace TeslaSolarCharger.Server.Helper;

/// <summary>
/// A car that is not charging does not start charging again while its SoC is close to its charge limit.
/// This is the single source of truth for that hysteresis, used for charge start and for charging targets.
/// </summary>
public static class CarChargeRestartHysteresis
{
    /// <summary>
    /// A car that is not charging only starts charging when its SoC is at least this many percent below the limit.
    /// </summary>
    public const int Percent = 3;

    /// <summary>
    /// True if a car that is not charging will not start charging towards <paramref name="socLimit"/> at <paramref name="soc"/>.
    /// </summary>
    public static bool PreventsChargeStart(int? soc, int? socLimit)
    {
        return soc > socLimit - Percent;
    }
}
