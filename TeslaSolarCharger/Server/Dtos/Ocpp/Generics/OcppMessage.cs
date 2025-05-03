using System.Text.Json.Serialization;

namespace TeslaSolarCharger.Server.Dtos.Ocpp.Generics;

/// <summary>
/// The part of every frame that is identical: MessageTypeId + UniqueId.
/// Concrete messages inherit from this.
/// </summary>
public abstract record OcppMessage
{
    [JsonPropertyOrder(0)]
    public MessageTypeId MessageTypeId { get; init; }

    /// <summary>
    /// Correlates request and response (sender‑chosen, unique per WS connection).
    /// </summary>
    [JsonPropertyOrder(1)]
    public string UniqueId { get; init; } = Guid.NewGuid().ToString("N");

    protected OcppMessage(MessageTypeId type) => MessageTypeId = type;
}
