namespace TeslaSolarCharger.SharedModel.Enums;

public enum HomeBatteryMode
{
    Unknown,
    /// <summary>
    /// Vendor default behavior, typically self consumption optimization.
    /// </summary>
    Normal,
    /// <summary>
    /// Battery is blocked from discharging.
    /// </summary>
    Hold,
    /// <summary>
    /// Battery is forced to charge, grid power is allowed.
    /// </summary>
    Charge,
}
