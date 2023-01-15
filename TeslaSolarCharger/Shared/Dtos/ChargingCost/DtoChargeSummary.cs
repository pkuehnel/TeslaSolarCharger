namespace TeslaSolarCharger.Shared.Dtos.ChargingCost;

public class DtoChargeSummary
{
    public decimal ChargedGridEnergy { get; set; }
    public decimal ChargedSolarEnergy { get; set; }
    public decimal ChargeCost { get; set; }
}
