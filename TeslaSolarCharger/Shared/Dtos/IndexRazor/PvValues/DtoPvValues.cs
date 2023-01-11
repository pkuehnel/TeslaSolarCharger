namespace TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;

public class DtoPvValues
{
    public double? InverterPower { get; set; }
    public double? GridPower { get; set; }
    public double? HomeBatteryPower { get; set; }
    public double? HomeBatterySoc { get; set; }
    public int? CarCombinedChargingPowerAtHome { get; set; }
    public DateTime LastUpdated { get; set; }
}
