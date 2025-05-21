using System.Text.Json.Serialization;

namespace TeslaSolarCharger.Server.Dtos.Ocpp;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RemoteStartStopStatus
{
    Accepted,
    Rejected,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChargingRateUnitType       // table 30
{
    W,
    A,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChargingProfilePurposeType // table 26
{
    ChargePointMaxProfile,
    TxDefaultProfile,
    TxProfile,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChargingProfileKindType    // table 26
{
    Absolute,
    Recurring,
    Relative,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RecurrencyKindType         // table 26
{
    Daily,
    Weekly,
}

// ──────────────────────────────────────────────────────────────────────
//  Nested structures for a ChargingProfile (tables 26‑31)
// ──────────────────────────────────────────────────────────────────────

public sealed record ChargingSchedulePeriod
{
    [JsonPropertyName("startPeriod")]
    public int StartPeriodSeconds { get; init; }

    [JsonPropertyName("limit")]
    public double Limit { get; init; }

    [JsonPropertyName("numberPhases")]
    public int? NumberPhases { get; init; }
}

public sealed record ChargingSchedule
{
    [JsonPropertyName("duration")]
    public int? DurationSeconds { get; init; }

    [JsonPropertyName("startSchedule")]
    public DateTime? StartSchedule { get; init; }

    [JsonPropertyName("chargingRateUnit")]
    public ChargingRateUnitType ChargingRateUnit { get; init; }

    [JsonPropertyName("chargingSchedulePeriod")]
    public IList<ChargingSchedulePeriod> ChargingSchedulePeriod { get; init; } =
        new List<ChargingSchedulePeriod>();

    [JsonPropertyName("minChargingRate")]
    public double? MinChargingRate { get; init; }
}

public sealed record ChargingProfile
{
    [JsonPropertyName("chargingProfileId")]
    public int ChargingProfileId { get; init; }

    [JsonPropertyName("transactionId")]
    public int? TransactionId { get; init; }

    [JsonPropertyName("stackLevel")]
    public int StackLevel { get; init; }

    [JsonPropertyName("chargingProfilePurpose")]
    public ChargingProfilePurposeType ChargingProfilePurpose { get; init; }

    [JsonPropertyName("chargingProfileKind")]
    public ChargingProfileKindType ChargingProfileKind { get; init; }

    [JsonPropertyName("recurrencyKind")]
    public RecurrencyKindType? RecurrencyKind { get; init; }

    [JsonPropertyName("validFrom")]
    public DateTime? ValidFrom { get; init; }

    [JsonPropertyName("validTo")]
    public DateTime? ValidTo { get; init; }

    [JsonPropertyName("chargingSchedule")]
    public ChargingSchedule ChargingSchedule { get; init; } = default!;
}

// ──────────────────────────────────────────────────────────────────────
//  RemoteStartTransaction.req  (central system → charge point)
// ──────────────────────────────────────────────────────────────────────

public sealed record RemoteStartTransactionRequest
{
    // required
    [JsonPropertyName("idTag")]
    public string IdTag { get; init; } = default!;         // ≤ 20 chars

    // optional
    [JsonPropertyName("connectorId")]
    public int? ConnectorId { get; init; }

    [JsonPropertyName("chargingProfile")]
    public ChargingProfile? ChargingProfile { get; init; }
}

// ──────────────────────────────────────────────────────────────────────
//  RemoteStartTransaction.conf  (charge point → central system)
// ──────────────────────────────────────────────────────────────────────

public sealed record RemoteStartTransactionResponse
{
    [JsonPropertyName("status")]
    public RemoteStartStopStatus Status { get; init; }
}

// ──────────────────────────────────────────────────────────────────────
//  RemoteStopTransaction.req  (central system → charge point)
// ──────────────────────────────────────────────────────────────────────


public sealed record RemoteStopTransactionRequest
{
    /// <summary>
    /// Number of the transaction to stop (required).
    /// </summary>
    [JsonPropertyName("transactionId")]
    public int TransactionId { get; init; }

    /// <summary>
    /// Optional idTag that should be used for authorization and in the
    /// resulting StopTransaction meter report (≤ 20 chars).
    /// </summary>
    [JsonPropertyName("idTag")]
    public string? IdTag { get; init; }
}

// ──────────────────────────────────────────────────────────────────────
//  RemoteStopTransaction.conf  (charge point → central system)
// ──────────────────────────────────────────────────────────────────────

public sealed record RemoteStopTransactionResponse
{
    /// <summary>
    /// Accepted if the CP has begun stopping the transaction, otherwise Rejected.
    /// </summary>
    [JsonPropertyName("status")]
    public RemoteStartStopStatus Status { get; init; }
}


// ──────────────────────────────────────────────────────────────────────
//  Enumeration used in SetChargingProfile.conf (table 33)
// ──────────────────────────────────────────────────────────────────────

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChargingProfileStatus
{
    Accepted,
    Rejected,
    NotSupported,
}

// ──────────────────────────────────────────────────────────────────────
//  SetChargingProfile.req  (central system → charge point)
// ──────────────────────────────────────────────────────────────────────

public sealed record SetChargingProfileRequest
{
    /// <summary>
    /// The connector to which the charging profile applies (0 = ChargePoint).
    /// </summary>
    [JsonPropertyName("connectorId")]
    public int ConnectorId { get; init; }

    /// <summary>
    /// A complete CSChargingProfiles object; we already modelled this as
    /// <see cref="ChargingProfile"/> (tables 26‑31).
    /// </summary>
    [JsonPropertyName("csChargingProfiles")]
    public ChargingProfile CsChargingProfiles { get; init; } = default!;
}

// ──────────────────────────────────────────────────────────────────────
//  SetChargingProfile.conf  (charge point → central system)
// ──────────────────────────────────────────────────────────────────────

public sealed record SetChargingProfileResponse
{
    [JsonPropertyName("status")]
    public ChargingProfileStatus Status { get; init; }
}

// ──────────────────────────────────────────────────────────────────────
//  MeterValues.conf  (central system → charge point)
//  – the specification defines an empty payload.
// ──────────────────────────────────────────────────────────────────────

public sealed record MeterValuesResponse
{
    // intentionally empty
}
