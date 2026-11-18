using TeslaSolarCharger.Server.Services.GridPrice.Dtos;
using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Server.Dtos.HomeBatteryControl;

/// <summary>
/// All data required to plan home battery hold/charge windows. Fully materialized so the planning core is pure and
/// testable without mocks.
/// </summary>
public class HomeBatteryPlanningInput
{
    public DateTimeOffset CurrentDate { get; set; }
    public int CurrentSocPercent { get; set; }
    /// <summary>Metered Wh to charge the battery from 0 to 100 %.</summary>
    public int ChargingEnergyWh { get; set; }
    /// <summary>Metered Wh the battery delivers from 100 to 0 %.</summary>
    public int DischargingEnergyWh { get; set; }
    /// <summary>Max power the battery can charge with. Null or 0 disables grid charge planning (hold only).</summary>
    public int? MaxChargingPowerW { get; set; }
    /// <summary>Max power the battery can discharge with. Null or 0: not limited.</summary>
    public int? MaxDischargingPowerW { get; set; }
    /// <summary>The battery is never planned to discharge below this SoC.</summary>
    public int MinSocPercent { get; set; }
    /// <summary>Highest SoC grid charging may fill the battery to.</summary>
    public int MaxChargeSocPercent { get; set; }
    /// <summary>
    /// Percent of the battery added to (or, when negative, taken from) the predicted house consumption grid charging
    /// may cover. Does not apply to car consumption.
    /// </summary>
    public int ChargeBufferPercent { get; set; }
    public DateTimeOffset SelfSufficiencyTime { get; set; }
    /// <summary>Additional costs per kWh taken from the battery (wear + charging losses).</summary>
    public decimal UsageCostsPerKwh { get; set; }
    public List<Price> GridPrices { get; set; } = new();
    /// <summary>Predicted solar production minus house consumption in Wh per slice, keyed by slice start.</summary>
    public Dictionary<DateTimeOffset, int> SurplusPerSlice { get; set; } = new();
    public List<DtoChargingSchedule> ChargingSchedules { get; set; } = new();
}
