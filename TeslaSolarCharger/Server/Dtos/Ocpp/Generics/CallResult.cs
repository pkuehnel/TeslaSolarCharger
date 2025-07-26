using System.Text.Json.Serialization;

namespace TeslaSolarCharger.Server.Dtos.Ocpp.Generics;

/// <summary>
/// Successful reply to a CALL. Position 2 = typed Payload.
/// </summary>
/// <typeparam name="TPayload">POCO that models the response payload.</typeparam>
public sealed record CallResult<TPayload>(
    string UniqueId,          // forwarded
    TPayload Payload)
    : OcppMessage(MessageTypeId.CallResult, UniqueId)
{
    [JsonPropertyOrder(2)]
    public TPayload Payload { get; init; } = Payload;
}
