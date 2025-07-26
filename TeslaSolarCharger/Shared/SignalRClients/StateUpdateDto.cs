namespace TeslaSolarCharger.Shared.SignalRClients;

public class StateUpdateDto
{
    public string DataType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public Dictionary<string, object?> ChangedProperties { get; set; } = new();
    public DateTimeOffset Timestamp { get; set; }
}
