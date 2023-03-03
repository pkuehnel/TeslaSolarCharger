namespace TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;

public class DtoPvValues
{
    public int? InverterPower { get; set; }
    public int? GridPower { get; set; }
    public int? HomeBatteryPower { get; set; }
    public int? HomeBatterySoc { get; set; }
    public int? CarCombinedChargingPowerAtHome { get; set; }
    public DateTimeOffset? LastUpdated { get; set; }
}
