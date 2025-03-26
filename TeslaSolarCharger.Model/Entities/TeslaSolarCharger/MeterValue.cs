namespace TeslaSolarCharger.Model.Entities.TeslaSolarCharger;

public class MeterValue
{
    public int Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public int? MeasuredPower { get; set; }
    public int? MeasuredEnergy { get; set; }
    public int? EstimatedPower { get; set; }
    public int? EstimatedEnergy { get; set; }
}
