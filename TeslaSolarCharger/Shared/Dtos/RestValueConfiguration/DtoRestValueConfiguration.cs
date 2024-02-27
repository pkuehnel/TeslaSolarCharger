using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;

public class DtoRestValueConfiguration
{
    public int Id { get; set; }
    public string Url { get; set; }
    public NodePatternType NodePatternType { get; set; }
    public HttpVerb HttpMethod { get; set; }
}
