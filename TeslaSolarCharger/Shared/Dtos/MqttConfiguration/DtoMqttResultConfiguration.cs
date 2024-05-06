using TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Dtos.MqttConfiguration;

public class DtoMqttResultConfiguration : DtoJsonXmlResultConfiguration
{
    public string Topic { get; set; }
    public NodePatternType NodePatternType { get; set; }
}
