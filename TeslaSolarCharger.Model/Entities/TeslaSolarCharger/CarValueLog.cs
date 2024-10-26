using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class CarValueLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public CarValueType Type { get; set; }
    public CarValueSource Source { get; set; }
    public double? DoubleValue { get; set; }
    public int? IntValue { get; set; }
    public string? StringValue { get; set; }
    public string? UnknownValue { get; set; }
    public bool? BooleanValue { get; set; }
    public bool? InvalidValue { get; set; }

    public int CarId { get; set; }
    public Car Car { get; set; }
}
