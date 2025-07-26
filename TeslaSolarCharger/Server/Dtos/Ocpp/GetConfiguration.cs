using System.Text.Json.Serialization;

namespace TeslaSolarCharger.Server.Dtos.Ocpp;

// ──────────────────────────────────────────────────────────────────
//  Nested structure – table 53 (ConfigurationKey)
// ──────────────────────────────────────────────────────────────────

/// <summary>
/// Represents a single configuration key/value pair returned
/// by GetConfiguration.conf.
/// </summary>
public sealed record KeyValue
{
    [JsonPropertyName("key")]
    public string Key { get; init; } = default!;          // ≤ 50 chars

    [JsonPropertyName("readonly")]
    public bool Readonly { get; init; }

    /// <summary>
    /// The value can be omitted if the key exists but has no value.
    /// </summary>
    [JsonPropertyName("value")]
    public string? Value { get; init; }                   // ≤ 500 chars
}

// ──────────────────────────────────────────────────────────────────
//  GetConfiguration.req  (charge point → central system)
// ──────────────────────────────────────────────────────────────────

public sealed record GetConfigurationRequest
{
    /// <summary>
    /// Optional list of specific keys the CP is interested in.
    /// If omitted, the CSO returns its full configuration set.
    /// </summary>
    [JsonPropertyName("key")]
    public IList<string>? Key { get; init; }
}

// ──────────────────────────────────────────────────────────────────
//  GetConfiguration.conf  (central system → charge point)
// ──────────────────────────────────────────────────────────────────

public sealed record GetConfigurationResponse
{
    /// <summary>
    /// All keys that were found and their values.
    /// </summary>
    [JsonPropertyName("configurationKey")]
    public IList<KeyValue>? ConfigurationKey { get; init; }

    /// <summary>
    /// Keys requested by the CP that do not exist in the CSO.
    /// </summary>
    [JsonPropertyName("unknownKey")]
    public IList<string>? UnknownKey { get; init; }
}
