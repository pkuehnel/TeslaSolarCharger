using System.ComponentModel.DataAnnotations;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;

public class DtoRestValueConfiguration
{
    public int Id { get; set; }
    [Required]
    [Url]
    public string Url { get; set; }
    public NodePatternType NodePatternType { get; set; }
    public HttpVerb HttpMethod { get; set; }
}
