using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Dtos.HomeBatteryControl;

/// <summary>
/// A planned time window in which the home battery should be set to a non default mode (Hold or Charge).
/// </summary>
public class DtoHomeBatteryScheduleWindow : ValidFromToBase
{
    public HomeBatteryMode Mode { get; set; }
    public HomeBatteryScheduleWindowReason Reason { get; set; }

    /// <summary>
    /// When set, the window is only applied while the current battery SoC is at or below this value, so energy the
    /// battery does not need until solar self sufficiency is not held back.
    /// </summary>
    public int? OnlyWhileSocAtOrBelowPercent { get; set; }

    /// <summary>
    /// For charge windows: once this SoC is reached, charging is demoted to hold for the rest of the window so the
    /// bought energy is preserved but no unneeded energy is bought.
    /// </summary>
    public int? TargetSocPercent { get; set; }

    /// <summary>
    /// For hold windows the predicted house consumption that is preserved in the battery, for charge windows the
    /// energy planned to be charged from the grid.
    /// </summary>
    public int PlannedEnergyWh { get; set; }

    /// <summary>
    /// Grid price during this window, for display purposes.
    /// </summary>
    public decimal GridPricePerKwh { get; set; }
}
