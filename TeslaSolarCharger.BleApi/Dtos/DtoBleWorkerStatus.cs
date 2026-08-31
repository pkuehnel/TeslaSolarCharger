namespace TeslaSolarCharger.BleApi.Dtos;

public class DtoBleWorkerStatus
{
    /// <summary>
    /// Canonical adapter key of the worker registry (the BD address when known).
    /// </summary>
    public string Adapter { get; set; } = string.Empty;
    public string HciId { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public bool UseDebug { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public double? UptimeSeconds { get; set; }
    public DateTimeOffset? LastRequestUtc { get; set; }
    public int RequestsSent { get; set; }
    public DateTimeOffset? KeepWarmUntil { get; set; }
    public double? SecondsUntilIdleStop { get; set; }
    public long? WorkerRssBytes { get; set; }
    /// <summary>
    /// CPU seconds the worker process consumed since it started (user plus system). Two samples divided by the time
    /// between them are the worker's CPU share, which is what a permanently scanning radio has to be judged by on a
    /// small device.
    /// </summary>
    public double? WorkerCpuSeconds { get; set; }
    public string? LastError { get; set; }
    public Dictionary<string, int> OutcomeCounts { get; set; } = new();
}
