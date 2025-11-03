using FluentValidation;
using System.ComponentModel.DataAnnotations;
using TeslaSolarCharger.Shared.Attributes;
using TeslaSolarCharger.Shared.Dtos.ChargingCost.CostConfigurations;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.ChargingCost;

public class DtoChargePrice
{
    public int? Id { get; set; }
    public DateTime ValidSince { get; set; }
    public string? EnergyProviderConfiguration { get; set; }
    [Required]
    public decimal? SolarPrice { get; set; }
    [Required]
    public decimal? GridPrice { get; set; }
    public bool AddSpotPriceToGridPrice { get; set; }
    public SpotPriceRegion? SpotPriceRegion { get; set; }
    [Postfix("%")]
    public decimal? SpotPriceSurcharge { get; set; } = 19;
}


public class DtoChargePriceValidator : AbstractValidator<DtoChargePrice>
{
    public DtoChargePriceValidator()
    {
        When(x => x.AddSpotPriceToGridPrice, () =>
        {
            RuleFor(x => x.SpotPriceRegion).NotEmpty();
            RuleFor(x => x.SpotPriceSurcharge).NotEmpty();
        });

    }
}
