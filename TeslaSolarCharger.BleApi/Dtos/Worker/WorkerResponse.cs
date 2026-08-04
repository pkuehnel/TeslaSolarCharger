using System.Text.Json;

namespace TeslaSolarCharger.BleApi.Dtos.Worker;

/// <summary>
/// One stdout line of the tesla-bled worker (see go/tesla-bled/main.go for the protocol). Deserialized with web
/// defaults (camelCase).
/// </summary>
public class WorkerResponse
{
    public string? Kind { get; set; }
    public int ProtocolVersion { get; set; }
    public string? AdapterId { get; set; }
    public int Id { get; set; }
    public bool Ok { get; set; }
    public string? Outcome { get; set; }
    public string? Phase { get; set; }
    public JsonElement? Result { get; set; }
    public string? Error { get; set; }
    public string? CarErrorMessage { get; set; }
    public WorkerScanInfo? Scan { get; set; }
    public WorkerBeaconScanInfo? BeaconScan { get; set; }
    /// <summary>Only set on the answer to a presence request.</summary>
    public WorkerPresenceInfo? Presence { get; set; }
    public long DurationMs { get; set; }
    public long ConnectMs { get; set; }
    public bool Reconnected { get; set; }
    public string? TimestampUtc { get; set; }
}

public class WorkerScanInfo
{
    public bool BeaconFound { get; set; }
    public int? Rssi { get; set; }
    public string? Address { get; set; }
    public bool? Connectable { get; set; }
    public int OtherAdvertisementsSeen { get; set; }
    public int DistinctDevicesSeen { get; set; }
    public long ScanDurationMs { get; set; }
}

public class WorkerBeaconScanInfo
{
    public int WindowMs { get; set; }
    public long ScanDurationMs { get; set; }
    public int OtherAdvertisementsSeen { get; set; }
    public int DistinctDevicesSeen { get; set; }
    public List<WorkerBeaconScanVehicle> Vehicles { get; set; } = new();
}

public class WorkerBeaconScanVehicle
{
    public string Vin { get; set; } = string.Empty;
    public bool BeaconFound { get; set; }
    public int? Rssi { get; set; }
    public string? Address { get; set; }
    public bool? Connectable { get; set; }
    public long? FoundAfterMs { get; set; }
    public long? LastHeardMsAgo { get; set; }
}

/// <summary>
/// State of the worker's permanent background beacon scan (see go/vehicle-command-ext/presence_scan.go).
/// </summary>
public class WorkerPresenceInfo
{
    public bool ScannerRunning { get; set; }
    public long ObservingMs { get; set; }
    public long ScanActiveMs { get; set; }
    public long PausedMs { get; set; }
    public long Restarts { get; set; }
    public long ScanErrors { get; set; }
    public string? LastScanError { get; set; }
    public long AdvertisementsSeen { get; set; }
    public int DistinctDevicesSeen { get; set; }
    public long? LastAdvertisementMsAgo { get; set; }
    public long MaxAgeMs { get; set; }
    public bool ScanWhileConnected { get; set; }
    public List<WorkerPresenceVehicle> Vehicles { get; set; } = new();
    public List<WorkerPresenceVehicle> Tracked { get; set; } = new();
}

public class WorkerPresenceVehicle
{
    public string? Vin { get; set; }
    public string LocalName { get; set; } = string.Empty;
    public bool Heard { get; set; }
    public long? LastHeardMsAgo { get; set; }
    public long? LastAdvertisementMsAgo { get; set; }
    public long? FirstHeardMsAgo { get; set; }
    public int? Rssi { get; set; }
    public string? Address { get; set; }
    public bool? Connectable { get; set; }
    public long Count { get; set; }
    public long NamedCount { get; set; }
    public long AddressCount { get; set; }
    public string? LastSource { get; set; }
    public List<long>? GapsMs { get; set; }
    public long MedianGapMs { get; set; }
    public long MaxGapMs { get; set; }
}
