using System.Text.Json.Serialization;

namespace TeslaSolarCharger.Server.Dtos.Ocpp;

// ──────────────────────────────────────────────────────────────────────
//  Enumerations and nested structures
// ──────────────────────────────────────────────────────────────────────

/// <summary>Table 40 – reasons why a transaction ended.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StopReason
{
    EmergencyStop,
    EVDisconnected,
    HardReset,
    Local,
    Other,
    PowerLoss,
    Reboot,
    Remote,
    SoftReset,
    UnlockCommand,
    DeAuthorized,
    EnergyLimitReached,
    GroundFault,
    HighTemperature,
    OvercurrentFailure
}



// ──────────────────────────────────────────────────────────────────────
//  StopTransaction.req  (charge point → central system)
// ──────────────────────────────────────────────────────────────────────

public sealed record StopTransactionRequest
{
    // required ------------------------------------------------------------
    [JsonPropertyName("transactionId")]
    public int TransactionId { get; init; }

    [JsonPropertyName("meterStop")]
    public int MeterStop { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTime TimestampUtc { get; init; }

    // optional ------------------------------------------------------------
    [JsonPropertyName("idTag")]
    public string? IdTag { get; init; }                       // ≤ 20 chars

    [JsonPropertyName("reason")]
    public StopReason? Reason { get; init; }

    [JsonPropertyName("transactionData")]
    public IList<MeterValue>? TransactionData { get; init; }
}

// ──────────────────────────────────────────────────────────────────────
//  StopTransaction.conf  (central system → charge point)
// ──────────────────────────────────────────────────────────────────────

public sealed record StopTransactionResponse
{
    /// <summary>
    /// Optional authorisation outcome for the idTag that ended the
    /// transaction.  Same object as used in StartTransaction.conf.
    /// </summary>
    [JsonPropertyName("idTagInfo")]
    public IdTagInfo? IdTagInfo { get; init; }
}
