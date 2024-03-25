using System.ComponentModel.DataAnnotations;

namespace TeslaSolarCharger.Server.Services.GridPrice.Options;

public class TibberOptions
{
    [Required]
    public string BaseUrl { get; set; }

    [Required]
    public string AccessToken { get; set; }
}
