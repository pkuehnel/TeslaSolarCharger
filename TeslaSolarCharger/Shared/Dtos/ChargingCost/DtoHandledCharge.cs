namespace TeslaSolarCharger.Shared.Dtos.ChargingCost;

public class DtoHandledCharge
{
    public decimal CalculatedPrice { get; set; }
    public decimal UsedGridEnergy { get; set; }
    public decimal UsedSolarEnergy { get; set; }
    public decimal GridPrice { get; set; }
    public decimal SolarPrice { get; set; }
    public decimal? AverageSpotPrice { get; set; }
}
