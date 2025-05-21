using System.Text.Json.Serialization;

namespace TeslaSolarCharger.Server.Dtos.Ocpp.Generics;

/// <summary>
/// Error reply. Positions 2–4 follow the spec.
/// </summary>
public sealed record CallError(string UniqueId,
    string ErrorCode,
    string ErrorDescription,
    object? ErrorDetails = null)
    : OcppMessage(MessageTypeId.CallError, UniqueId)
{
    [JsonPropertyOrder(1)]
    public new string UniqueId { get; init; } = UniqueId;

    [JsonPropertyOrder(2)]
    public string ErrorCode { get; init; } = ErrorCode;

    [JsonPropertyOrder(3)]
    public string ErrorDescription { get; init; } = ErrorDescription;

    [JsonPropertyOrder(4)]
    public object? ErrorDetails { get; init; } = ErrorDetails;
}
