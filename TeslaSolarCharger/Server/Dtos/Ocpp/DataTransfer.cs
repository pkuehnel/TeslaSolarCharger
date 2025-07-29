using System.Text.Json.Serialization;

namespace TeslaSolarCharger.Server.Dtos.Ocpp;

// ──────────────────────────────────────────────────────────────────
//  Enumeration for DataTransfer.conf – table 17
// ──────────────────────────────────────────────────────────────────
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DataTransferStatus
{
    Accepted,
    Rejected,
    UnknownMessageId,
    UnknownVendorId
}

// ──────────────────────────────────────────────────────────────────
//  DataTransfer.req  (either side → the other)
// ──────────────────────────────────────────────────────────────────
public sealed record DataTransferRequest
{
    /// <summary>
    /// Vendor that defines the proprietary message (≤ 255 chars, required).
    /// </summary>
    [JsonPropertyName("vendorId")]
    public string VendorId { get; init; } = default!;

    /// <summary>
    /// Optional message identifier defined by the vendor (≤ 50 chars).
    /// </summary>
    [JsonPropertyName("messageId")]
    public string? MessageId { get; init; }

    /// <summary>
    /// Optional opaque data blob (≤ 512 chars, UTF-8 JSON-encoded string).
    /// </summary>
    [JsonPropertyName("data")]
    public string? Data { get; init; }
}

// ──────────────────────────────────────────────────────────────────
//  DataTransfer.conf  (reply)
// ──────────────────────────────────────────────────────────────────
public sealed record DataTransferResponse
{
    /// <summary>
    /// Overall outcome of the proprietary request.
    /// </summary>
    [JsonPropertyName("status")]
    public DataTransferStatus Status { get; init; }

    /// <summary>
    /// Optional proprietary data the recipient returns (≤ 512 chars).
    /// </summary>
    [JsonPropertyName("data")]
    public string? Data { get; init; }
}
