using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.TimeSeries;

public class DtoTimeSeriesDatum
{
    public DateTime Timestamp { get; set; }
    public double? Value { get; set; }
}
