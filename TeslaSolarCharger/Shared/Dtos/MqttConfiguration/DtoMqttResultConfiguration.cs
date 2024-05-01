using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Dtos.MqttConfiguration;

public class DtoMqttResultConfiguration
{
    public int Id { get; set; }
    public decimal CorrectionFactor { get; set; }
    public ValueUsage UsedFor { get; set; }
    public ValueOperator Operator { get; set; }
    public NodePatternType NodePatternType { get; set; }
    public string? NodePattern { get; set; }
    public string? XmlAttributeHeaderName { get; set; }
    public string? XmlAttributeHeaderValue { get; set; }
    public string? XmlAttributeValueName { get; set; }
}
