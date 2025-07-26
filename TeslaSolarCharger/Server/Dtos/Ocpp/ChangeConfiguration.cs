using System.Text.Json.Serialization;

namespace TeslaSolarCharger.Server.Dtos.Ocpp;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConfigurationStatus
{
    Accepted,
    Rejected,
    RebootRequired,
    NotSupported,
}

// ──────────────────────────────────────────────────────────────────
//  ChangeConfiguration.req  (central system → charge point)
// ──────────────────────────────────────────────────────────────────

public sealed record ChangeConfigurationRequest
{
    /// <summary>Configuration key to change (≤ 50 chars).</summary>
    [JsonPropertyName("key")]
    public string Key { get; init; } = default!;

    /// <summary>
    /// New value for the key (≤ 500 chars).  
    /// May be an empty string if the key supports that.
    /// </summary>
    [JsonPropertyName("value")]
    public string Value { get; init; } = default!;
}

// ──────────────────────────────────────────────────────────────────
//  ChangeConfiguration.conf  (charge point → central system)
// ──────────────────────────────────────────────────────────────────

public sealed record ChangeConfigurationResponse
{
    [JsonPropertyName("status")]
    public ConfigurationStatus Status { get; init; }
}
