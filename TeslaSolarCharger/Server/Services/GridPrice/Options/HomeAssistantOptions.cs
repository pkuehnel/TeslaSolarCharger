using System.ComponentModel.DataAnnotations;

namespace TeslaSolarCharger.Server.Services.GridPrice.Options;

public class HomeAssistantOptions
{
    [Required]
    public string BaseUrl { get; set; }

    [Required]
    public string AccessToken { get; set; }

    [Required]
    public string EntityId { get; set; }
}
