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
}
