namespace TeslaSolarCharger.BleApi.Dtos;

/// <summary>
/// What the permanent background beacon scan of one adapter's worker knows. Container local on purpose: this is the
/// bench instrument for the scan rework, not part of the TSC wire contract, so it can change without a lockstep
/// version bump.
/// </summary>
public class DtoBleScannerStatus
{
    public string? Adapter { get; set; }
    public bool ScannerRunning { get; set; }
    /// <summary>Wall clock time the scanner existed, whether or not it was allowed to scan.</summary>
    public long ObservingMs { get; set; }
    /// <summary>Time actually spent inside a scan. The rest went to commands, connects and pairing.</summary>
    public long ScanActiveMs { get; set; }
    public long PausedMs { get; set; }
    /// <summary>Share of the scanner's lifetime it really listened. The number the whole rework stands or falls by.</summary>
    public double DutyCyclePercent { get; set; }
    /// <summary>How often the deafness watchdog had to re-arm the scan.</summary>
    public long Restarts { get; set; }
    public long ScanErrors { get; set; }
    public string? LastScanError { get; set; }
    public long AdvertisementsSeen { get; set; }
    public double AdvertisementsPerSecond { get; set; }
    public int DistinctDevicesSeen { get; set; }
    public long? LastAdvertisementMsAgo { get; set; }
    public long MaxAgeMs { get; set; }
    public bool ScanWhileConnected { get; set; }
    /// <summary>The cars that were asked about, in the order they were requested.</summary>
    public List<DtoBleScannerVehicle> Vehicles { get; set; } = new();
    /// <summary>Every car the radio heard, including ones nobody asked about (a neighbour's Tesla shows up here).</summary>
    public List<DtoBleScannerVehicle> Tracked { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class DtoBleScannerVehicle
{
    public string? Vin { get; set; }
    public string LocalName { get; set; } = string.Empty;
    /// <summary>True when the car was heard within the max age the question was asked with.</summary>
    public bool Heard { get; set; }
    public long? LastHeardMsAgo { get; set; }
    /// <summary>Last advertisement only; a command that reached the car updates LastHeardMsAgo but not this.</summary>
    public long? LastAdvertisementMsAgo { get; set; }
    public long? FirstHeardMsAgo { get; set; }
    public int? Rssi { get; set; }
    public string? Address { get; set; }
    public bool? Connectable { get; set; }
    public long Count { get; set; }
    /// <summary>Advertisements that carried the car's local name.</summary>
    public long NamedCount { get; set; }
    /// <summary>
    /// Advertisements recognized only by the learned address. A high share means the local name travels in the scan
    /// response and the old name-only windowed scan was discarding most of the car's advertisements.
    /// </summary>
    public long AddressCount { get; set; }
    public string? LastSource { get; set; }
    /// <summary>Gaps between consecutive advertisements, oldest first: the measured advertising cadence.</summary>
    public List<long>? GapsMs { get; set; }
    public long MedianGapMs { get; set; }
    public long MaxGapMs { get; set; }
}
