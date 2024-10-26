using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Dtos;

public class DtoTscFleetTelemetryMessage
{
    public CarValueType Type { get; set; }
    public double? DoubleValue { get; set; }
    public int? IntValue { get; set; }
    public string? StringValue { get; set; }
    public string? UnknownValue { get; set; }
    public bool? BooleanValue { get; set; }
    public bool? InvalidValue { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
}
