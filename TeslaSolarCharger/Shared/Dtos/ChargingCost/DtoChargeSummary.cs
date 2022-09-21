namespace TeslaSolarCharger.Shared.Dtos.ChargingCost;

public class DtoChargeSummary
{
    public int CarId { get; set; }
    public decimal ChargedGridEnergy { get; set; }
    public decimal ChargedSolarEnergy { get; set; }
    public decimal ChargeCost { get; set; }
}
