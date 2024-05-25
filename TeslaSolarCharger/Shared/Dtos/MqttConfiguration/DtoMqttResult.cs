using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Dtos.MqttConfiguration;

public class DtoMqttResult
{
    public ValueUsage UsedFor { get; set; }
    public decimal Value { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
    public string Key { get; set; }
}
