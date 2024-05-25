namespace TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;
using System.ComponentModel.DataAnnotations;

public class DtoFullRestValueConfiguration : DtoRestValueConfiguration
{
    [ValidateComplexType]
    public List<DtoRestValueConfigurationHeader> Headers { get; set; } = new();
}
