namespace TeslaSolarCharger.SharedBackend.Dtos;

//Attention: this also is implemented in espace TeslaSolarCharger.Shared.Dtos.IndexRazor.PvValues. Can not be combined as this would result in UI needing all dependecies of SharedBackend project
public class DtoCurrentPvValues
{
    public int? InverterPower { get; set; }
    public int? GridPower { get; set; }
    public int? HomeBatteryPower { get; set; }
    public int? HomeBatterySoc { get; set; }
    public DateTimeOffset? LastUpdated { get; set; }
}
