namespace TeslaSolarCharger.Shared.Dtos.HomeBatteryControl;

public enum HomeBatteryScheduleWindowReason
{
    /// <summary>
    /// Planned car grid charging the battery should not cover (its energy is saved for more expensive slices or the grid
    /// price is below the battery usage costs), so the battery is held.
    /// </summary>
    CarGridCharging,
    /// <summary>
    /// House consumption the battery should not cover for the same reasons, so the house runs on grid and the battery
    /// is held.
    /// </summary>
    PreserveForDeficit,
    /// <summary>
    /// The battery is charged from the grid in a cheap time slice to cover house consumption or planned car grid charging
    /// in later, more expensive slices. Used for every planned charge window.
    /// </summary>
    GridChargeForDeficit,
}
