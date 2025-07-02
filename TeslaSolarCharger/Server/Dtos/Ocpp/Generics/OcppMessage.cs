using System.Text.Json.Serialization;

namespace TeslaSolarCharger.Server.Dtos.Ocpp.Generics;

/// <summary>
/// The part of every frame that is identical: MessageTypeId + UniqueId.
/// Concrete messages inherit from this.
/// </summary>
public abstract record OcppMessage(
    MessageTypeId MessageTypeId,
    string UniqueId)     // ← one and only UniqueId
{
    protected OcppMessage(MessageTypeId messageTypeId)
        : this(messageTypeId, Guid.NewGuid().ToString("N")) { }


    [JsonPropertyOrder(0)]
    public MessageTypeId MessageTypeId { get; init; } = MessageTypeId;

    [JsonPropertyOrder(1)]
    public string UniqueId { get; init; } = UniqueId;
}
