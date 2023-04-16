namespace TeslaSolarCharger.Shared.Dtos.ChargingCost;

public class DtoHandledCharge
{
    public int ChargingProcessId { get; set; }
    public DateTime? StartTime { get; set; }
    public decimal CalculatedPrice { get; set; }
    public decimal PricePerKwh { get; set; }
    public decimal UsedGridEnergy { get; set; }
    public decimal UsedSolarEnergy { get; set; }
    public decimal? AverageSpotPrice { get; set; }
}
