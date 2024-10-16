using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class CarValueLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public CarValueType Type { get; set; }
    public double? DoubleValue { get; set; }
    public int? IntValue { get; set; }

    public int CarId { get; set; }
    public Car Car { get; set; }
}
