using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class SpotPrice
{
    public int Id { get; set; }
    public DateTime StartDate { get; set; }
    public decimal Price { get; set; }
    public SpotPriceRegion? SpotPriceRegion { get; set; }
}
