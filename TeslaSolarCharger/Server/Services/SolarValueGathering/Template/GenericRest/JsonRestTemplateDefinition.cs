using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.SolarValueGathering.Template.GenericRest;

public class JsonRestTemplateDefinition
{
    public required List<JsonRestValueRead> ValueReads { get; init; }
    public JsonRestAuthType ValueAuthType { get; init; }
    public RestBatteryControlDefinition? BatteryControl { get; init; }
}

public class JsonRestValueRead
{
    /// <summary>
    /// Supported placeholders: {host}, {port}, {deviceId}
    /// </summary>
    public required string UriTemplate { get; init; }
    public required List<JsonRestValue> Values { get; init; }
}

public class JsonRestValue
{
    public required ValueUsage UsedFor { get; init; }
    /// <summary>
    /// Newtonsoft JSONPath, e.g. $.data.p1 or $.dxsEntries[?(@.dxsId == 33556736)].value
    /// </summary>
    public required string JsonPath { get; init; }
    public ValueOperator Operator { get; init; } = ValueOperator.Plus;
    public decimal CorrectionFactor { get; init; } = 1;
}

public enum JsonRestAuthType
{
    None,
    Basic,
    TokenHeader,
}

public class RestBatteryControlDefinition
{
    public TimeSpan? RewriteInterval { get; init; }
    public JsonRestAuthType AuthType { get; init; }
    /// <summary>
    /// Name of the header the API token is sent in when <see cref="AuthType"/> is TokenHeader.
    /// </summary>
    public string? TokenHeaderName { get; init; }
    public required List<RestBatteryModeRequest> NormalRequests { get; init; }
    public required List<RestBatteryModeRequest> HoldRequests { get; init; }
    public required List<RestBatteryModeRequest> ChargeRequests { get; init; }

    public List<RestBatteryModeRequest> GetRequests(HomeBatteryMode mode)
    {
        var requests = mode switch
        {
            HomeBatteryMode.Normal => NormalRequests,
            HomeBatteryMode.Hold => HoldRequests,
            HomeBatteryMode.Charge => ChargeRequests,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Mode can not be written"),
        };
        //An empty request list marks the mode as not supported by the device.
        if (requests.Count == 0)
        {
            throw new NotSupportedException($"Mode {mode} is not supported by this device");
        }
        return requests;
    }
}

public class RestBatteryModeRequest
{
    public required string Method { get; init; }
    /// <summary>
    /// Supported placeholders: {host}, {port}, {maxChargePowerW}
    /// </summary>
    public required string UriTemplate { get; init; }
    /// <summary>
    /// Optional JSON body. Supported placeholders: {maxChargePowerW}
    /// </summary>
    public string? JsonBodyTemplate { get; init; }

    public static RestBatteryModeRequest Get(string uriTemplate) => new() { Method = "GET", UriTemplate = uriTemplate };
    public static RestBatteryModeRequest Post(string uriTemplate, string? jsonBodyTemplate = null) =>
        new() { Method = "POST", UriTemplate = uriTemplate, JsonBodyTemplate = jsonBodyTemplate };
    public static RestBatteryModeRequest Put(string uriTemplate, string? jsonBodyTemplate = null) =>
        new() { Method = "PUT", UriTemplate = uriTemplate, JsonBodyTemplate = jsonBodyTemplate };
}
