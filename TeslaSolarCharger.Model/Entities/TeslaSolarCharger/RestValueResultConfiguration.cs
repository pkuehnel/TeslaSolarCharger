using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class RestValueResultConfiguration
{
    public int Id { get; set; }
    public string? NodePattern { get; set; }
    public float CorrectionFactor { get; set; }
    public ValueUsage UsedFor { get; set; }
    public ValueOperator Operator { get; set; }

    public int RestValueConfigurationId { get; set; }
    public RestValueConfiguration RestValueConfiguration { get; set; }
}
