using System.ComponentModel.DataAnnotations;

namespace TeslaSolarCharger.Server.Services.GridPrice.Options;

public class AwattarOptions
{
    [Required]
    public string BaseUrl { get; set; }

    public decimal VATMultiplier { get; set; }
}
