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
        DefaultReason = reason;
    }

    public DtoNotChargingWithExpectedPowerReason(string localizationKey, string defaultReason)
    {
        LocalizationKey = localizationKey;
        Reason = defaultReason;
        DefaultReason = defaultReason;
    }

    public DtoNotChargingWithExpectedPowerReason(string localizationKey, string defaultReason, DateTimeOffset? reasonEndTime)
        : this(localizationKey, defaultReason)
    {
        ReasonEndTime = reasonEndTime;
    }

    public DtoNotChargingWithExpectedPowerReason(string reason, DateTimeOffset? reasonEndTime) : this(reason)
    {
        ReasonEndTime = reasonEndTime;
    }

    public string Reason { get; set; }
    public string? LocalizationKey { get; set; }
    public string? DefaultReason { get; set; }
    public DateTimeOffset? ReasonEndTime { get; set; }
}
