namespace TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;

public class DtoFullRestValueConfiguration : DtoRestValueConfiguration
{
    public List<DtoRestValueConfigurationHeader> Headers { get; set; } = new List<DtoRestValueConfigurationHeader>();
    public List<DtoRestValueResultConfiguration> RestValueResultConfigurations { get; set; } = new List<DtoRestValueResultConfiguration>();
}
