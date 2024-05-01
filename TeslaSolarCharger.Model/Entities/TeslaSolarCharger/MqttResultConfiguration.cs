using TeslaSolarCharger.Model.BaseClasses;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class MqttResultConfiguration : JsonXmlResultConfigurationBase
{
    public NodePatternType NodePatternType { get; set; }

    public int MqttConfigurationId { get; set; }
    public MqttConfiguration MqttConfiguration { get; set; } = null!;
}
