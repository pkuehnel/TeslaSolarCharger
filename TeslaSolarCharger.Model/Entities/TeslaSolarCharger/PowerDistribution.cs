namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class PowerDistribution
{
    public int Id { get; set; }
    public DateTime TimeStamp { get; set; }
    public int CharingPower { get; set; }
    public int PowerFromGrid { get; set; }
    public float GridProportion { get; set; }

    public int HandledChargeId { get; set; }
    public HandledCharge HandledCharge { get; set; }
}
