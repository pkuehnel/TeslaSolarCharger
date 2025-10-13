using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

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

    [JsonIgnore]
    public string? LocalizationKey { get; set; }

    [JsonIgnore]
    public List<object?>? LocalizationArguments { get; set; }

    public static DtoNotChargingWithExpectedPowerReason Create(string localizationKey,
        DateTimeOffset? reasonEndTime = null,
        params object?[] localizationArguments)
    {
        return new()
        {
            LocalizationKey = localizationKey,
            ReasonEndTime = reasonEndTime,
            LocalizationArguments = localizationArguments?.ToList()
        };
    }

    public DtoNotChargingWithExpectedPowerReason CloneWithReason(string reason)
    {
        return new(reason, ReasonEndTime)
        {
            LocalizationKey = LocalizationKey,
            LocalizationArguments = LocalizationArguments?.ToList()
        };
    }
}
