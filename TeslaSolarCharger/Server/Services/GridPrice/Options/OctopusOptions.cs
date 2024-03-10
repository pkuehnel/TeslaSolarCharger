using System.ComponentModel.DataAnnotations;

namespace TeslaSolarCharger.Server.Services.GridPrice.Options;

public class OctopusOptions
{
    [Required]
    public string BaseUrl { get; set; }

    [Required]
    public string ProductCode { get; set; }

    [Required]
    public string TariffCode { get; set; }

    [Required]
    public string RegionCode { get; set; }
}
