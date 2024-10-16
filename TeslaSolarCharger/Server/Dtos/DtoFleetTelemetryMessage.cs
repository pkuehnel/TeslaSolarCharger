using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Dtos;

public class DtoFleetTelemetryMessage
{
    public CarValueType Type { get; set; }
    public double? DoubleValue { get; set; }
    public int? IntValue { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}
