namespace TeslaSolarCharger.Server.Dtos.ChargingServiceV2;

public class DtoEstimatedProduction
{
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public int ProducedWh { get; set; }
}
