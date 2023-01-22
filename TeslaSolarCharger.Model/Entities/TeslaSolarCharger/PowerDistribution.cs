namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class PowerDistribution
{
    public int Id { get; set; }
    public DateTime TimeStamp { get; set; }
    public int ChargingPower { get; set; }
    public int PowerFromGrid { get; set; }
    public float GridProportion { get; set; }
    public float? UsedWattHours { get; set; }

    public int HandledChargeId { get; set; }
#pragma warning disable CS8618
    public HandledCharge HandledCharge { get; set; }
#pragma warning restore CS8618
}
