using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Dtos.BaseConfiguration;

public class DtoOverviewValueResult
{
    public int Id { get; set; }
    public ValueUsage UsedFor { get; set; }

    /// <summary>
    /// The value together with when it was last refreshed, or null while the device has not delivered anything for
    /// this result yet.
    /// </summary>
    public DtoTimeStampedValue<decimal>? Value { get; set; }
}
