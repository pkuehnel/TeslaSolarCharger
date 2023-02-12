using System.ComponentModel.DataAnnotations;

namespace TeslaSolarCharger.Shared.Dtos.ChargingCost;

public class DtoChargePrice
{
    public int? Id { get; set; }
    public DateTime ValidSince { get; set; }
    [Required]
    public decimal? SolarPrice { get; set; }
    [Required]
    public decimal? GridPrice { get; set; }
    public bool AddSpotPriceToGridPrice { get; set; }
    public decimal? SpotPriceSurcharge { get; set; } = (decimal?)0.19;
}
