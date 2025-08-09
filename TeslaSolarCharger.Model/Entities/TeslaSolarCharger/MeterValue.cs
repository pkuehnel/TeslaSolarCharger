using TeslaSolarCharger.Model.Enums;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class MeterValue
{
    public int Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public MeterValueKind MeterValueKind { get; set; }
    public int? MeasuredPower { get; set; }
    public long? MeasuredEnergyWs { get; set; }
    public int? EstimatedPower { get; set; }
    public long? EstimatedEnergyWs { get; set; }

    public int? CarId { get; set; }

    public Car? Car { get; set; }
}
