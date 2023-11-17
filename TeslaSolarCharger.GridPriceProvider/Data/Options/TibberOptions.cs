using System.ComponentModel.DataAnnotations;

namespace TeslaSolarCharger.GridPriceProvider.Data.Options;

public class TibberOptions
{
    [Required]
    public string BaseUrl { get; set; }

    [Required]
    public string AccessToken { get; set; }
}
