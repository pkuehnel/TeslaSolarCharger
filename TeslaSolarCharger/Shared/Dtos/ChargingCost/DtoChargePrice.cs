namespace TeslaSolarCharger.Shared.Dtos.ChargingCost;

public class DtoChargePrice
{
    public int? Id { get; set; }
    public DateTime ValidSince { get; set; }
    public decimal SolarPrice { get; set; }
    public decimal GridPrice { get; set; }
}
