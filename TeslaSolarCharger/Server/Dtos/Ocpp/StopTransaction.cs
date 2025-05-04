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

// The following two records reproduce the MeterValue / SampledValue
// objects used in transactionData (tables 56‑58).  Only the required
// fields are marked non‑nullable.  Extend as needed.

public sealed record SampledValue
{
    [JsonPropertyName("value")]
    public string Value { get; init; } = default!;            // required

    // optional fields -----------------------------------------------------
    [JsonPropertyName("context")]
    public string? Context { get; init; }

    [JsonPropertyName("format")]
    public string? Format { get; init; }

    [JsonPropertyName("measurand")]
    public string? Measurand { get; init; }

    [JsonPropertyName("phase")]
    public string? Phase { get; init; }

    [JsonPropertyName("location")]
    public string? Location { get; init; }

    [JsonPropertyName("unit")]
    public string? Unit { get; init; }
}

public sealed record MeterValue
{
    [JsonPropertyName("timestamp")]
    public DateTime TimestampUtc { get; init; }               // required

    [JsonPropertyName("sampledValue")]
    public IList<SampledValue> SampledValue { get; init; } =
        new List<SampledValue>();                            // ≥ 1 required
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
