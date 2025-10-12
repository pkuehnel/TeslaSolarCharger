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

    public static DtoNotChargingWithExpectedPowerReason CreateLocalized(string resourceKey, params string[] formatParameters)
    {
        return new DtoNotChargingWithExpectedPowerReason()
        {
            ResourceKey = resourceKey,
            FormatParameters = formatParameters.ToList(),
        };
    }

    public static DtoNotChargingWithExpectedPowerReason CreateLocalized(string resourceKey, DateTimeOffset? reasonEndTime, params string[] formatParameters)
    {
        var reason = CreateLocalized(resourceKey, formatParameters);
        reason.ReasonEndTime = reasonEndTime;
        return reason;
    }

    public string? Reason { get; set; }
    public string? ResourceKey { get; set; }
    public List<string> FormatParameters { get; set; } = new();
    public DateTimeOffset? ReasonEndTime { get; set; }

    [JsonIgnore]
    public bool HasResourceKey => !string.IsNullOrWhiteSpace(ResourceKey);
}
