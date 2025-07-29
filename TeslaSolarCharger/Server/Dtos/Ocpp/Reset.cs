using System.Text.Json.Serialization;

namespace TeslaSolarCharger.Server.Dtos.Ocpp;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResetType          // used in Reset.req
{
    Hard,
    Soft,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResetStatus        // used in Reset.conf
{
    Accepted,
    Rejected,
}

// ───────────────────────────────────────────────────────────────
//  Reset.req  (central system → charge point)
// ───────────────────────────────────────────────────────────────

public sealed record ResetRequest
{
    /// <summary>
    /// The kind of reboot the CP should perform (Hard = power cycle,
    /// Soft = software restart).
    /// </summary>
    [JsonPropertyName("type")]
    public ResetType Type { get; init; }
}

// ───────────────────────────────────────────────────────────────
//  Reset.conf  (charge point → central system)
// ───────────────────────────────────────────────────────────────

public sealed record ResetResponse
{
    /// <summary>
    /// Accepted if the CP will reset, Rejected otherwise.
    /// </summary>
    [JsonPropertyName("status")]
    public ResetStatus Status { get; init; }
}
