namespace PkSoftwareService.Custom.Backend.Ble;

/// <summary>
/// What the BLE container knows about the cars it was asked about, answered from the permanent background scan
/// without touching the radio. Replaces the beacon scan: the question is no longer "was the car heard while we
/// listened for n seconds" but "how long ago was it last heard".
/// </summary>
public class DtoBlePresenceResult
{
    public string? Adapter { get; set; }
    /// <summary>False when the adapter is not being listened to at all, which carries no presence information.</summary>
    public bool ScannerRunning { get; set; }
    /// <summary>
    /// True while the scan has not been observing for a full max age yet, e.g. right after a container or worker
    /// restart. Nothing may be concluded about any car: not heard yet is not the same as not there.
    /// </summary>
    public bool WarmingUp { get; set; }
    public long ObservingMs { get; set; }
    public long MaxAgeMs { get; set; }
    public long AdvertisementsSeen { get; set; }
    public double AdvertisementsPerSecond { get; set; }
    public int DistinctDevicesSeen { get; set; }
    /// <summary>
    /// How long ago the radio last received anything at all from any device. The evidence that a silent radio is
    /// still a working one; null when it never heard anything.
    /// </summary>
    public long? LastAdvertisementMsAgo { get; set; }
    public string? LastScanError { get; set; }
    /// <summary>The cars that were asked about, in the order they were requested.</summary>
    public List<DtoBlePresenceVehicle> Vehicles { get; set; } = new();
    /// <summary>Every car the radio heard, including ones nobody asked about. Diagnostic only.</summary>
    public List<DtoBlePresenceVehicle> Tracked { get; set; } = new();
    /// <summary>Set when the container could not answer at all. Never means "car is away".</summary>
    public string? ErrorMessage { get; set; }
}

public class DtoBlePresenceVehicle
{
    public string? Vin { get; set; }
    public string LocalName { get; set; } = string.Empty;
    /// <summary>True when the car was heard within the max age, by advertisement or by a command it answered.</summary>
    public bool Heard { get; set; }
    /// <summary>Age of the newest evidence of any kind. This is what presence is decided on.</summary>
    public long? LastSeenMsAgo { get; set; }
    /// <summary>
    /// Advertisements only. A car emits none while it holds a connection to us, so this ages during a poll while
    /// <see cref="LastCommandSuccessMsAgo"/> stays fresh - the two sources are complementary by design.
    /// </summary>
    public long? LastAdvertisementMsAgo { get; set; }
    public long? LastCommandSuccessMsAgo { get; set; }
    public long? FirstHeardMsAgo { get; set; }
    public int? Rssi { get; set; }
    public string? Address { get; set; }
    public bool? Connectable { get; set; }
    public long Count { get; set; }
    /// <summary>Advertisements that carried the car's local name.</summary>
    public long NamedCount { get; set; }
    /// <summary>
    /// Advertisements recognized only by the learned address. Measured at 55-61 % of both cars' traffic, so a name
    /// only matcher throws most of a car's advertisements away.
    /// </summary>
    public long AddressCount { get; set; }
    /// <summary>advertisement, address or command.</summary>
    public string? LastSource { get; set; }
}
