namespace TeslaSolarCharger.Shared.Dtos.HomeBatteryControl;

public enum HomeBatteryScheduleWindowReason
{
    /// <summary>
    /// A car is intentionally charged from the grid, so the home battery is held to not discharge into the car.
    /// </summary>
    CarGridCharging,
    /// <summary>
    /// The battery would not last until solar self sufficiency and the grid price is below the battery energy costs,
    /// so the house runs on grid to preserve battery energy.
    /// </summary>
    PreserveForDeficit,
    /// <summary>
    /// The battery would not last until solar self sufficiency even with holds, so it is charged from the grid at cheap prices.
    /// </summary>
    GridChargeForDeficit,
}
