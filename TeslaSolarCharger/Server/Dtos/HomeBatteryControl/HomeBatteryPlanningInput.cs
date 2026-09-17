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
    public int UsableEnergyWh { get; set; }
    /// <summary>Max power the battery can charge with. Null disables grid charge planning (hold only).</summary>
    public int? MaxChargingPowerW { get; set; }
    /// <summary>SoC the battery needs right now to last until self sufficiency (hold buffer).</summary>
    public int HoldTargetSocPercent { get; set; }
    /// <summary>SoC up to which grid charging may fill the battery (charge buffer, usually below the hold target).</summary>
    public int ChargeTargetSocPercent { get; set; }
    public DateTimeOffset SelfSufficiencyTime { get; set; }
    /// <summary>Additional costs per kWh taken from the battery (wear + charging losses).</summary>
    public decimal UsageCostsPerKwh { get; set; }
    public List<Price> GridPrices { get; set; } = new();
    /// <summary>Predicted solar production minus house consumption in Wh per slice, keyed by slice start.</summary>
    public Dictionary<DateTimeOffset, int> SurplusPerSlice { get; set; } = new();
    public List<DtoChargingSchedule> ChargingSchedules { get; set; } = new();
}
