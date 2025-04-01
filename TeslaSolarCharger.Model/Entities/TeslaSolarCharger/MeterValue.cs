using TeslaSolarCharger.Model.Enums;

namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class MeterValue
{
    public int Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    //When adding another key, like CarId to separate meter values for different cars, make sure that is used everywhere. Recommendation is to use a separate class for that
    public MeterValueKind MeterValueKind { get; set; }
    public int? MeasuredPower { get; set; }
    public long? MeasuredEnergyWs { get; set; }
    public int? EstimatedPower { get; set; }
    public long? EstimatedEnergyWs { get; set; }
}
