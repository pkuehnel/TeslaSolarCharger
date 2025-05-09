using System.Text.Json.Serialization;

namespace TeslaSolarCharger.Server.Dtos.Ocpp;

// ──────────────────────────────────────────────────────────────────────
//  Enumerations and nested structures used in StartTransaction.conf
// ──────────────────────────────────────────────────────────────────────

/// <summary>Table 6 – values that can appear in an IdTagInfo.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuthorizationStatus
{
    Accepted,
    Blocked,
    Expired,
    Invalid,
    ConcurrentTx
}

/// <summary>
/// Object returned by the CSO to indicate whether the provided idTag
/// is (still) authorised for a transaction.
/// </summary>
public sealed record IdTagInfo
{
    [JsonPropertyName("status")]
    public AuthorizationStatus Status { get; init; }

    [JsonPropertyName("expiryDate")]
    public DateTime? ExpiryDateUtc { get; init; }

    /// <summary>
    /// Parent idTag, if the CSO uses hierarchical authorisation (≤ 20 chars).
    /// </summary>
    [JsonPropertyName("parentIdTag")]
    public string? ParentIdTag { get; init; }
}

// ──────────────────────────────────────────────────────────────────────
//  StartTransaction.req  (charge point → central system)
// ──────────────────────────────────────────────────────────────────────

public sealed record StartTransactionRequest
{
    // required ------------------------------------------------------------
    [JsonPropertyName("connectorId")]
    public int ConnectorId { get; init; }

    [JsonPropertyName("idTag")]
    public string IdTag { get; init; } = default!;          // ≤ 20 chars

    [JsonPropertyName("meterStart")]
    public int MeterStart { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTime TimestampUtc { get; init; }

    // optional ------------------------------------------------------------
    [JsonPropertyName("reservationId")]
    public int? ReservationId { get; init; }
}

// ──────────────────────────────────────────────────────────────────────
//  StartTransaction.conf  (central system → charge point)
// ──────────────────────────────────────────────────────────────────────

public sealed record StartTransactionResponse
{
    [JsonPropertyName("transactionId")]
    public int TransactionId { get; init; }

    [JsonPropertyName("idTagInfo")]
    public IdTagInfo IdTagInfo { get; init; } = default!;
}
