namespace PkSoftwareService.Custom.Backend.Ble;

public class DtoBleBeaconVehicleResult
{
    public string Vin { get; set; } = string.Empty;
    public bool BeaconFound { get; set; }
    public int? Rssi { get; set; }
    public string? Address { get; set; }
    public bool? Connectable { get; set; }
    /// <summary>
    /// Milliseconds into the scan window at which the beacon was first heard.
    /// </summary>
    public long? FoundAfterMs { get; set; }
}
