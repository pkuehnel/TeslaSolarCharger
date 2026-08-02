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
}
