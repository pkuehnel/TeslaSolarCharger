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
    /// How old the newest evidence about a car may be and still count as present. Both its BLE advertisements and the
    /// commands it answered count: a Tesla emits nothing at all while it holds a connection, so the two sources cover
    /// exactly the periods the other cannot. Measured worst case under a real 13 s poll was 21 s, and the worst a
    /// parked car went unheard in an idle hour was 0.6 s, so 90 s is generous. The away confirmation sits on top of
    /// this, putting the away transition at about four minutes of true silence.
    /// </summary>
    public const int BlePresenceMaxAgeSeconds = 90;
}
