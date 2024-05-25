using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

public class DtoOverviewValueResult
{
    public int Id { get; set; }
    public ValueUsage UsedFor { get; set; }
    public decimal? CalculatedValue { get; set; }
}
