namespace TeslaSolarCharger.BleApi.Dtos;

public class DtoBleWorkerEvent
{
    public DateTimeOffset TimestampUtc { get; set; }
    public string Adapter { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
