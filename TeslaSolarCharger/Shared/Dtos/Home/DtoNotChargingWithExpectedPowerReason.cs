namespace TeslaSolarCharger.Shared.Dtos.Home;

public class DtoNotChargingWithExpectedPowerReason
{
    //Required for serialization
#pragma warning disable CS8618, CS9264
    public DtoNotChargingWithExpectedPowerReason()
#pragma warning restore CS8618, CS9264
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
