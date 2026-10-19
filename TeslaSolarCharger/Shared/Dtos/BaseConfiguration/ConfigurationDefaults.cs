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
    /// exactly the periods the other cannot. A car not heard for this long counts as away, provided the scan has been
    /// running for at least this long.
    /// </summary>
    public const int BlePresenceMaxAgeSeconds = 180;

    /// <summary>
    /// Percentage points added to the dynamically calculated state of charge above which the home battery is held
    /// rather than discharged. Zero holds at exactly the calculated level.
    /// </summary>
    public const int HoldHomeBatteryChargeSocBuffer = 0;

    /// <summary>
    /// Percentage points added to the dynamically calculated state of charge above which the home battery is charged
    /// from the grid. Negative on purpose: charging from the grid is only worth it well below the level that merely
    /// needs holding.
    /// </summary>
    public const int ChargeHomeBatterySocBuffer = -25;
}
