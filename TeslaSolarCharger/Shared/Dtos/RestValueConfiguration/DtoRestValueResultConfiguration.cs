using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;

public class DtoRestValueResultConfiguration
{
    public int Id { get; set; }
    public string? NodePattern { get; set; }
    public string? XmlAttributeHeaderName { get; set; }
    public string? XmlAttributeHeaderValue { get; set; }
    public string? XmlAttributeValueName { get; set; }
    public decimal CorrectionFactor { get; set; } = 1;
    public ValueUsage UsedFor { get; set; }
    public ValueOperator Operator { get; set; }
}
