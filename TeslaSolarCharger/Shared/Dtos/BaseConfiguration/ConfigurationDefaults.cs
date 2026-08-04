namespace TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

/// <summary>
/// Central place for the current default value of configuration settings that are stored as nullable so the default
/// can be changed between releases without overwriting an explicit user choice. A nullable setting resolves to the
/// value here when the user never set it (see the corresponding <c>ConfigurationWrapper</c> getters). Changing a
/// default for a future release is a one line edit here and only affects users who never set the value themselves.
/// </summary>
public static class ConfigurationDefaults
{
    /// <summary>
    /// Seconds between two BLE data refresh runs. The refresh is decoupled from the charging cycle, so a car that is
    /// slow to answer (or absent) can no longer delay the charging value calculation.
    /// </summary>
    public const int BleDataRefreshIntervalSeconds = 13;

    /// <summary>
    /// Minutes an idle BLE car is not polled via the infotainment system so its standby timer can run out and it can
    /// fall asleep. VCSEC (body controller state) polling continues. A single infotainment poll happens when the
    /// window expires. 0 disables the whole BLE sleep window feature.
    /// </summary>
    public const int BleSleepWindowMinutes = 13;

    /// <summary>
    /// Minutes of unchanged BLE polls (doors/frunk/trunk closed and unchanged, plugged in state, charge limit and no
    /// occupant) before a BLE sleep window starts.
    /// </summary>
    public const int BleSleepStabilityMinutes = 5;

    /// <summary>
    /// Consecutive failed BLE beacon scans before a car's presence counts as uncertain and charging commands are
    /// suspended. 1 suspends on the very first miss. A weak radio misses scans on a car that is provably at home, and
    /// every miss then blocks charging control until the next hit, so a small tolerance keeps a marginal link usable.
    /// This never delays the away detection itself, which has its own (longer) confirmation window.
    /// </summary>
    public const int BleMissesBeforePresenceUncertain = 2;

    /// <summary>
    /// Seconds a beacon scan listens for a car before giving up. Measured on a real car, a parked Tesla advertises
    /// only every ten seconds or so, so a short window misses it most of the time even at good signal strength. The
    /// scan ends the moment every car was heard, so a longer window costs nothing while the car is there and only
    /// takes its full length when a car really is away.
    /// </summary>
    public const int BleBeaconScanWindowSeconds = 7;
}
