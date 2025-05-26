namespace TeslaSolarCharger.Server.Dtos.ChargingServiceV2;

public class DtoTimeZonedChargingTarget
{
    public int Id { get; set; }
    public DateTimeOffset NextExecutionTime { get; set; }
}
