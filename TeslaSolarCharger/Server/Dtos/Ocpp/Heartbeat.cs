using System.Text.Json.Serialization;

namespace TeslaSolarCharger.Server.Dtos.Ocpp;

/// <summary>
/// Heartbeat.req – sent by the charge point.
/// The payload is empty, so the class has no properties.
/// </summary>
public sealed record HeartbeatRequest
{
    // Intentionally left blank – no fields defined by the spec.
}

/// <summary>
/// Heartbeat.conf – returned by the central system.
/// </summary>
public sealed record HeartbeatResponse
{
    /// <summary>
    /// Current time at the central system, in RFC 3339 / ISO 8601 UTC format.
    /// </summary>
    [JsonPropertyName("currentTime")]
    public DateTime CurrentTimeUtc { get; init; }
}
