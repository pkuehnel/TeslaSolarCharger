using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Dtos.RestValueConfiguration;

public class DtoRestValueResultConfiguration
{
    public int Id { get; set; }
    public string? NodePattern { get; set; }
    public float CorrectionFactor { get; set; }
    public ValueUsage UsedFor { get; set; }
    public ValueOperator Operator { get; set; }
}
