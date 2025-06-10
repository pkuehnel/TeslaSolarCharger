namespace TeslaSolarCharger.Shared.Dtos.Home;

public class DtoChargeStartStopReason
{
    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? StopTime { get; set; }
}
