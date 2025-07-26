using System.Text.Json.Serialization;

namespace TeslaSolarCharger.Server.Dtos.Ocpp;

public sealed record AuthorizeRequest
{
    /// <summary>
    /// The RFID or other token presented by the user (≤ 20 chars).
    /// </summary>
    [JsonPropertyName("idTag")]
    public string IdTag { get; init; } = default!;
}

// ──────────────────────────────────────────────────────────────────
//  Authorize.conf  (central system → charge point)
// ──────────────────────────────────────────────────────────────────

public sealed record AuthorizeResponse
{
    /// <summary>
    /// Authorisation information for the supplied idTag.
    /// </summary>
    [JsonPropertyName("idTagInfo")]
    public IdTagInfo IdTagInfo { get; init; } = default!;
}
