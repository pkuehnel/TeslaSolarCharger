using System.Text.Json.Serialization;

namespace TeslaSolarCharger.Server.Dtos.Ocpp.Generics;

/// <summary>
/// A CALL frame sent by either side.
/// Position 2 = Action, Position 3 = typed Payload.
/// </summary>
/// <typeparam name="TPayload">POCO that models the request payload.</typeparam>
public sealed record Call<TPayload>(string Action, TPayload Payload)
    : OcppMessage(MessageTypeId.Call)
{
    [JsonPropertyOrder(2)]
    public string Action { get; init; } = Action;

    [JsonPropertyOrder(3)]
    public TPayload Payload { get; init; } = Payload;
}
