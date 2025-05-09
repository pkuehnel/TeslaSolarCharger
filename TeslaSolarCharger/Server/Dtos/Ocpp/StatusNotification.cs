using System.Text.Json.Serialization;

namespace TeslaSolarCharger.Server.Dtos.Ocpp;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChargePointStatus
{
    Available,
    Preparing,
    Charging,
    SuspendedEVSE,
    SuspendedEV,
    Finishing,
    Reserved,
    Unavailable,
    Faulted
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChargePointErrorCode
{
    ConnectorLockFailure,
    EVCommunicationError,
    GroundFailure,
    HighTemperature,
    InternalError,
    LocalListConflict,
    NoError,
    OtherError,
    OverCurrentFailure,
    PowerMeterFailure,
    PowerSwitchFailure,
    ReaderFailure,
    ResetFailure,
    UnderVoltage,
    OverVoltage,
    WeakSignal
}

// ---------------------------------------------------------------------
//  StatusNotification.req  (sent by charge point)
// ---------------------------------------------------------------------

public sealed record StatusNotificationRequest
{
    // --- required --------------------------------------------------------
    [JsonPropertyName("connectorId")]
    public int ConnectorId { get; init; }

    [JsonPropertyName("errorCode")]
    public ChargePointErrorCode ErrorCode { get; init; }

    [JsonPropertyName("status")]
    public ChargePointStatus Status { get; init; }

    // --- optional --------------------------------------------------------
    /// <summary>Extra vendor‑specific info (≤ 50 chars).</summary>
    [JsonPropertyName("info")]
    public string? Info { get; init; }

    /// <summary>Time of the event, RFC 3339 / ISO 8601 format, UTC.</summary>
    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; init; }

    /// <summary>Vendor id for proprietary error codes (≤ 255 chars).</summary>
    [JsonPropertyName("vendorId")]
    public string? VendorId { get; init; }

    /// <summary>Vendor‑specific error code (≤ 50 chars).</summary>
    [JsonPropertyName("vendorErrorCode")]
    public string? VendorErrorCode { get; init; }
}

// ---------------------------------------------------------------------
//  StatusNotification.conf (sent by central system) – payload is empty
// ---------------------------------------------------------------------

public sealed record StatusNotificationResponse
{
    // Intentionally left blank – spec defines no fields.
}
