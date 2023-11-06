using System.ComponentModel.DataAnnotations;

namespace TeslaSolarCharger.GridPriceProvider.Data.Options;

public class FixedPriceOptions
{
    public List<string> Prices { get; set; } = new();
}
