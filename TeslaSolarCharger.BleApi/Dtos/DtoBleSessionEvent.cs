namespace TeslaSolarCharger.BleApi.Dtos;

/// <summary>
/// One timestamped event of a held tesla-control session, e.g. a sent command, received output or a process exit.
/// </summary>
public class DtoBleSessionEvent
{
    public DateTimeOffset TimestampUtc { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
