namespace TeslaSolarCharger.Shared.Dtos.ChargingCost;

public class DtoHandledCharge
{
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal CalculatedPrice { get; set; }
    public decimal PricePerKwh { get; set; }
    public decimal UsedGridEnergy { get; set; }
    public decimal UsedHomeBatteryEnergy { get; set; }
    public decimal UsedSolarEnergy { get; set; }
}
