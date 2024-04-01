using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Dtos;

public abstract class ValueConfigurationBase
{
    public int Id { get; set; }
    public decimal CorrectionFactor { get; set; } = 1;
    public ValueUsage UsedFor { get; set; }
    public ValueOperator Operator { get; set; }
}
