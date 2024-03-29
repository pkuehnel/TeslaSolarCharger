namespace TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;

public class DtoFullRestValueConfiguration : DtoRestValueConfiguration
{
    public List<DtoRestValueConfigurationHeader> Headers { get; set; } = new();
}
