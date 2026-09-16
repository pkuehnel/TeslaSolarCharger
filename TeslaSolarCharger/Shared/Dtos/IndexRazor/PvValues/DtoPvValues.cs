using TeslaSolarCharger.Shared.Attributes;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;

//Attention: this also is implemented in TeslaSolarCharger.SharedBackend.Dtos. Can not be combined as this would result in UI needing all dependecies of SharedBackend project
public class DtoPvValues
{
    /// <summary>
    /// Every device's current contribution. The totals below are worked out from this list, so they can never
    /// disagree with the values per device.
    /// </summary>
    public List<DtoPvSourceValue> SourceValues { get; set; } = new();

    public int? InverterPower => ValueFor(ValueUsage.InverterPower);
    public int? GridPower => ValueFor(ValueUsage.GridPower);
    public int? HomeBatteryPower => ValueFor(ValueUsage.HomeBatteryPower);
    public int? HomeBatterySoc => ValueFor(ValueUsage.HomeBatterySoc);
    [Postfix("W")]
    public int? PowerBuffer { get; set; }
    public int? CarCombinedChargingPowerAtHome { get; set; }
    public DateTimeOffset? LastUpdated { get; set; }

    /// <summary>
    /// The total of every device that supplies <paramref name="usage"/>, or null when no device supplies it.
    /// </summary>
    public int? ValueFor(ValueUsage usage)
    {
        var values = SourceValues.Where(v => v.UsedFor == usage).ToList();
        if (values.Count == 0)
        {
            return null;
        }

        var total = (int)Math.Min(Math.Max(values.Sum(v => v.Value), int.MinValue), int.MaxValue);
        //An inverter's own standby draw is not solar generation.
        if (usage == ValueUsage.InverterPower && total < 0)
        {
            return 0;
        }

        return total;
    }
}
