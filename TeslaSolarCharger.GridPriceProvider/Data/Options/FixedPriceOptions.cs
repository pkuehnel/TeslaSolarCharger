using System.ComponentModel.DataAnnotations;

namespace TeslaSolarCharger.GridPriceProvider.Data.Options;

public class FixedPriceOptions
{
    [Required]
    public string TimeZone { get; set; }

    [Required]
    public List<string> Prices { get; set; }
}
