using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using TeslaSolarCharger.Shared.Attributes;
using TeslaSolarCharger.Shared.Dtos.ChargingCost.CostConfigurations;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.ChargingCost;

public class DtoChargePrice
{
    [DisplayName("ID")]
    public int? Id { get; set; }

    [DisplayName("Valid Since")]
    public DateTime ValidSince { get; set; }

    [DisplayName("Energy Provider")]
    public EnergyProvider EnergyProvider { get; set; } = EnergyProvider.FixedPrice;
    public string? EnergyProviderConfiguration { get; set; }
    [Required]
    [DisplayName("Solar Price")]
    public decimal? SolarPrice { get; set; }
    [Required]
    [DisplayName("Grid Price")]
    public decimal? GridPrice { get; set; }
    public bool AddSpotPriceToGridPrice { get; set; }
    [DisplayName("Additional costs to spotprice")]
    [HelperText("Surcharge to spot price (e.g. aWATTar 3% + 19% VAT in Germany). Note: Spot prices are without VAT.")]
    [Postfix("%")]
    public decimal? SpotPriceSurcharge { get; set; } = 19;
}
