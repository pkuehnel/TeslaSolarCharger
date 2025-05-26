namespace TeslaSolarCharger.Server.Dtos.ChargingServiceV2;

public class DtoChargingSchedule
{
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }

    public int ChargingPower { get; set; }
    public decimal ChargingCurrent { get; set; }
    public int NumberOfPhases { get; set; }
}
