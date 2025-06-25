namespace TeslaSolarCharger.Shared.Dtos.Home;

public class DtoNotChargingWithExpectedPowerReason
{
    //Required for serialization
    public DtoNotChargingWithExpectedPowerReason()
    {
        
    }
    public DtoNotChargingWithExpectedPowerReason(string reason)
    {
        Reason = reason;
    }

    public DtoNotChargingWithExpectedPowerReason(string reason, DateTimeOffset? reasonEndTime) : this(reason)
    {
        ReasonEndTime = reasonEndTime;
    }

    public string Reason { get; set; }
    public DateTimeOffset? ReasonEndTime { get; set; }
}
