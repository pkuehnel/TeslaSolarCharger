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
}
