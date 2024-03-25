using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class RestValueConfiguration
{
    public int Id { get; set; }
    public string Url { get; set; }
    public NodePatternType NodePatternType { get; set; }
    public HttpVerb HttpMethod { get; set; }

    public List<RestValueConfigurationHeader> Headers { get; set; } = new();
    public List<RestValueResultConfiguration> RestValueResultConfigurations { get; set; } = new();
}
