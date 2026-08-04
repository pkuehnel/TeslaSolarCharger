namespace TeslaSolarCharger.Shared.Dtos.Ble;

/// <summary>
/// One beacon scan result for one car, as it was observed. TSC only acts on <see cref="BeaconFound"/>, but the
/// container reports far more per scan and that detail is what makes an unreliable BLE link diagnosable: a car that
/// is heard at a strong RSSI but only every few scans is a quiet car, while weak or absent RSSI on a car known to be
/// there points at the radio.
/// </summary>
public class DtoBleBeaconObservation
{
    public DateTimeOffset Timestamp { get; set; }
    public bool BeaconFound { get; set; }
    /// <summary>Signal strength in dBm of the advertisement that was heard. Null when the car was not found.</summary>
    public int? Rssi { get; set; }
    /// <summary>Milliseconds into the scan window at which the car was first heard. Null when it was not found.</summary>
    public long? FoundAfterMs { get; set; }
    /// <summary>How long the scan was allowed to listen.</summary>
    public int ScanWindowMs { get; set; }
    /// <summary>How long it actually listened. Shorter than the window when every car was heard early.</summary>
    public long ScanDurationMs { get; set; }
    /// <summary>
    /// Advertisements from other devices during the same scan. A miss with a high count means the radio was working
    /// and the car stayed quiet; a miss with zero means nothing at all was received.
    /// </summary>
    public int OtherAdvertisementsSeen { get; set; }
    public string? Adapter { get; set; }
}

/// <summary>
/// The recorded beacon observations of one car plus the figures that answer "is this link healthy".
/// </summary>
public class DtoBleBeaconHistory
{
    public List<DtoBleBeaconObservation> Observations { get; set; } = new();
    public int TotalScans { get; set; }
    public int FoundScans { get; set; }
    /// <summary>Share of scans that heard the car, 0 to 100. Null when nothing was recorded yet.</summary>
    public double? HitRatePercent { get; set; }
    /// <summary>Mean RSSI over the scans that heard the car. Null when the car was never heard.</summary>
    public double? AverageRssi { get; set; }
    /// <summary>Longest run of consecutive scans that did not hear the car, which is what suspends charging.</summary>
    public int LongestMissStreak { get; set; }
    public DateTimeOffset? LastFoundAt { get; set; }
}
