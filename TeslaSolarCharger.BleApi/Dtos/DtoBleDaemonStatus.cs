namespace TeslaSolarCharger.BleApi.Dtos;

/// <summary>
/// State of the long living BLE worker process.
/// </summary>
public class DtoBleDaemonStatus
{
    public bool IsRunning { get; set; }
    public bool UseDebug { get; set; }
    public string? Arguments { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public double? UptimeSeconds { get; set; }
    public DateTimeOffset? LastRequestUtc { get; set; }
    public int RequestsSent { get; set; }
    public string? LastError { get; set; }

    /// <summary>
    /// Seconds until the daemon stops itself because no request came in, or null if it is not running.
    /// </summary>
    public double? SecondsUntilIdleStop { get; set; }
}
