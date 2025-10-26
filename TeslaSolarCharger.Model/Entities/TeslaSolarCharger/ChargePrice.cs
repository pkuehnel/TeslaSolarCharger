using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class ChargePrice
{
    public int Id { get; set; }
    public DateTime ValidSince { get; set; }
    public string? EnergyProviderConfiguration { get; set; }
    public decimal SolarPrice { get; set; }
    public decimal GridPrice { get; set; }
    public bool AddSpotPriceToGridPrice { get; set; }
    //Let this nullable forever as otherwise a default region would be set
    public SpotPriceRegion? SpotPriceRegion { get; set; }
    public decimal SpotPriceCorrectionFactor { get; set; }
}
