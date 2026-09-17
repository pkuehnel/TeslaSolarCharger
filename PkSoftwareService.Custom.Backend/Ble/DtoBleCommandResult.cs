namespace PkSoftwareService.Custom.Backend.Ble;

public class DtoBleCommandResult
{
    /// <summary>
    /// Human readable result or error text. Never parsed anywhere; classification travels in <see cref="Outcome"/>.
    /// </summary>
    public string? ResultMessage { get; set; }
    public bool Success { get; set; }
    public ErrorType? ErrorType { get; set; }
    public string? CarErrorMessage { get; set; }
    /// <summary>
    /// Structured classification of the result. Null when the answering container predates version 2.40.0; callers
    /// must treat null as "no presence information" and never fall back to parsing <see cref="ResultMessage"/>.
    /// </summary>
    public BleCommandOutcome? Outcome { get; set; }
    public BleCommandPhase? Phase { get; set; }
    /// <summary>
    /// Whether the car's beacon was seen during this request. True implies the car is present even if the request
    /// failed afterwards.
    /// </summary>
    public bool? BeaconFound { get; set; }
    /// <summary>
    /// Advertisements from other devices heard during the scan. Evidence that the radio receives at all.
    /// </summary>
    public int? OtherAdvertisementsSeen { get; set; }
    public int? DistinctDevicesSeen { get; set; }
    public long? ScanDurationMs { get; set; }
    /// <summary>
    /// Time spent establishing the vehicle connection; 0 when an existing connection was reused.
    /// </summary>
    public long? ConnectMs { get; set; }
    public long? DurationMs { get; set; }
}
