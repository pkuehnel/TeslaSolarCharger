using System.Text.Json.Serialization;

namespace TeslaSolarCharger.Server.Dtos.Ocpp;

/// <summary>
/// The request payload that a charge point sends in a BootNotification CALL frame.
/// </summary>
public sealed record BootNotificationRequest
{
    // --- required fields ---------------------------------------------------
    [JsonPropertyName("chargePointVendor")]
    public string ChargePointVendor { get; init; } = default!;   // max 20 chars

    [JsonPropertyName("chargePointModel")]
    public string ChargePointModel { get; init; } = default!;   // max 20 chars

    // --- optional fields ---------------------------------------------------
    [JsonPropertyName("chargePointSerialNumber")]
    public string? ChargePointSerialNumber { get; init; }        // max 25 chars

    [JsonPropertyName("chargeBoxSerialNumber")]
    public string? ChargeBoxSerialNumber { get; init; }         // max 25 chars

    [JsonPropertyName("firmwareVersion")]
    public string? FirmwareVersion { get; init; }         // max 50 chars

    [JsonPropertyName("iccid")]
    public string? Iccid { get; init; }         // max 20 chars

    [JsonPropertyName("imsi")]
    public string? Imsi { get; init; }         // max 20 chars

    [JsonPropertyName("meterType")]
    public string? MeterType { get; init; }         // max 25 chars

    [JsonPropertyName("meterSerialNumber")]
    public string? MeterSerialNumber { get; init; }         // max 25 chars
}


/// <summary>
/// Values allowed in BootNotification.conf
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RegistrationStatus
{
    Accepted,
    Pending,
    Rejected,
}

/// <summary>
/// The confirmation payload the central system returns in a BootNotification CALLRESULT frame.
/// </summary>
public sealed record BootNotificationResponse
{
    [JsonPropertyName("status")]
    public RegistrationStatus Status { get; init; }

    /// <summary>
    /// Current time at the central system, in RFC 3339 / ISO 8601 UTC format.
    /// </summary>
    [JsonPropertyName("currentTime")]
    public DateTime CurrentTimeUtc { get; init; }

    /// <summary>
    /// Heartbeat interval the charge point shall use (seconds).
    /// </summary>
    [JsonPropertyName("interval")]
    public int IntervalSeconds { get; init; }
}
