namespace PkSoftwareService.Custom.Backend.Ble;

public class DtoBleBeaconScanResult
{
    public bool Success { get; set; }
    /// <summary>
    /// <see cref="BleCommandOutcome.Ok"/> when the scan itself ran (regardless of which cars were found); any other
    /// value means the scan could not run and no vehicle entry carries presence information.
    /// </summary>
    public BleCommandOutcome? Outcome { get; set; }
    public string? ResultMessage { get; set; }
    public int WindowMs { get; set; }
    public long ScanDurationMs { get; set; }
    public int OtherAdvertisementsSeen { get; set; }
    public int DistinctDevicesSeen { get; set; }
    public List<DtoBleBeaconVehicleResult> Vehicles { get; set; } = new();
}
