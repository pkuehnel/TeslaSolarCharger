using System.ComponentModel.DataAnnotations;
using TeslaSolarCharger.Shared.Dtos.ChargingCost.CostConfigurations;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.ChargingCost;

public class DtoChargePrice
{
    public int? Id { get; set; }
    public DateTime ValidSince { get; set; }
    public EnergyProvider EnergyProvider { get; set; } = EnergyProvider.FixedPrice;
    public string? EnergyProviderConfiguration { get; set; }
    public List<FixedPrice>? FixedPrices { get; set; }
    [Required]
    public decimal? SolarPrice { get; set; }
    [Required]
    public decimal? GridPrice { get; set; }
    public bool AddSpotPriceToGridPrice { get; set; }
    public decimal? SpotPriceSurcharge { get; set; } = 19;
}
