namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class HandledCharge
{
    public int Id { get; set; }
    public int ChargingProcessId { get; set; }
    public int CarId { get; set; }
    public decimal? UsedGridEnergy { get; set; }
    public decimal? UsedSolarEnergy { get; set; }
    public decimal? CalculatedPrice { get; set; }
    public List<PowerDistribution> PowerDistributions { get; set; } = new();


    public int ChargePriceId { get; set; }
    public ChargePrice ChargePrice { get; set; }
}
