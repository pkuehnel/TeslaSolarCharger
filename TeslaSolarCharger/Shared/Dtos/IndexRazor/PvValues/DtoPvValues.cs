namespace TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues;

//Attention: this also is implemented in TeslaSolarCharger.SharedBackend.Dtos. Can not be combined as this would result in UI needing all dependecies of SharedBackend project
public class DtoPvValues
{
    public int? InverterPower { get; set; }
    public int? GridPower { get; set; }
    public int? HomeBatteryPower { get; set; }
    public int? HomeBatterySoc { get; set; }
    public int? PowerBuffer { get; set; }
    public int? CarCombinedChargingPowerAtHome { get; set; }
    public DateTimeOffset? LastUpdated { get; set; }
}
