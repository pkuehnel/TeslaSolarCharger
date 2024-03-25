namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class ChargingDetail
{
    public int Id { get; set; }
    public DateTime TimeStamp { get; set; }
    public int SolarPower { get; set; }
    public int GridPower { get; set; }

    public int ChargingProcessId { get; set; }

    public ChargingProcess ChargingProcess { get; set; } = null!;
}
