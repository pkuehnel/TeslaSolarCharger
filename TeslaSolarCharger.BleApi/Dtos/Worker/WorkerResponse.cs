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
    public long DurationMs { get; set; }
    public long ConnectMs { get; set; }
    public bool Reconnected { get; set; }
    public string? TimestampUtc { get; set; }

    //Fields of the unsolicited lines. They share this type so there is exactly one parse path for everything the
    //worker writes; "kind" decides which of them carry meaning.
    /// <summary>kind "adv": length of the reported window.</summary>
    public long WindowMs { get; set; }
    /// <summary>kind "adv": every advertisement received in the window, including from devices dropped by the cap.</summary>
    public int Total { get; set; }
    /// <summary>kind "adv": set when the device cap dropped devices from this window.</summary>
    public bool Truncated { get; set; }
    /// <summary>kind "adv": one entry per Bluetooth address heard in the window.</summary>
    public List<WorkerDeviceObservation>? Devices { get; set; }
    /// <summary>kind "scan": running, paused, error or stopped.</summary>
    public string? State { get; set; }
    /// <summary>kind "scan": why the state changed.</summary>
    public string? Reason { get; set; }
}

/// <summary>
/// What one Bluetooth address emitted during one digest window. <see cref="Named"/> against <see cref="Count"/> is
/// the split that lets the container learn a car's address: most of a Tesla's advertisements carry no local name.
/// </summary>
public class WorkerDeviceObservation
{
    public string Addr { get; set; } = string.Empty;
    public string? Name { get; set; }
    public int Rssi { get; set; }
    public int Count { get; set; }
    public int Named { get; set; }
    public bool Connectable { get; set; }
}

