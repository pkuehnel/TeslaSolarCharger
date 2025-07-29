using System.Text.Json.Serialization;

namespace TeslaSolarCharger.Server.Dtos.Ocpp;

// ──────────────────────────────────────────────────────────────────
//  Enumerations defined in the Remote-Trigger profile (tables 61-62)
// ──────────────────────────────────────────────────────────────────

/// <summary>
/// Name of the message the central system wants the charge point to emit.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RequestedMessage
{
    BootNotification,
    DiagnosticsStatusNotification,
    FirmwareStatusNotification,
    Heartbeat,
    MeterValues,
    StatusNotification,
}

/// <summary>
/// Result returned in TriggerMessage.conf.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TriggerMessageStatus
{
    Accepted,
    Rejected,
    NotImplemented,
}

// ──────────────────────────────────────────────────────────────────
//  TriggerMessage.req  (central system → charge point)
// ──────────────────────────────────────────────────────────────────

public sealed record TriggerMessageRequest
{
    /// <summary>
    /// The OCPP message the charge point shall send immediately.
    /// </summary>
    [JsonPropertyName("requestedMessage")]
    public RequestedMessage RequestedMessage { get; init; }

    /// <summary>
    /// Optional connector for which to trigger the message.
    /// Only meaningful for StatusNotification or MeterValues.
    /// </summary>
    [JsonPropertyName("connectorId")]
    public int? ConnectorId { get; init; }
}

// ──────────────────────────────────────────────────────────────────
//  TriggerMessage.conf  (charge point → central system)
// ──────────────────────────────────────────────────────────────────

public sealed record TriggerMessageResponse
{
    [JsonPropertyName("status")]
    public TriggerMessageStatus Status { get; init; }
}
