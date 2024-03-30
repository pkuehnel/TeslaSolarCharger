using System.ComponentModel.DataAnnotations;

namespace TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;

public class DtoRestValueConfigurationHeader
{
    public int Id { get; set; }
    [Required]
    public string Key { get; set; }
    public string Value { get; set; }
}
