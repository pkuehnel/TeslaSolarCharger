namespace TeslaSolarCharger.Shared.Dtos.Ble;

/// <summary>
/// What was known about one car at one poll. TSC only acts on <see cref="IsPresent"/>, but the container reports more
/// and that detail is what makes an unreliable BLE link diagnosable: a car heard at a strong RSSI is a healthy link,
/// while presence carried only by commands means the car is silent - normally because our own connection silences it.
/// </summary>
public class DtoBleBeaconObservation
{
    public DateTimeOffset Timestamp { get; set; }
    /// <summary>Whether the newest evidence was inside the configured max age.</summary>
    public bool IsPresent { get; set; }
    /// <summary>Age of the newest evidence of any kind. Null when the car was never heard.</summary>
    public long? LastSeenMsAgo { get; set; }
    /// <summary>
    /// How presence was proven: an advertisement, an advertisement matched by the car's learned address, or a command
    /// the car answered. Null while nothing is known.
    /// </summary>
    public string? EvidenceSource { get; set; }
    /// <summary>Signal strength in dBm of the last advertisement heard. Null when the car never advertised.</summary>
    public int? Rssi { get; set; }
    /// <summary>
    /// Advertisements the container's radio received from all devices. A car that is not present while this is high
    /// means the radio was working and the car was gone; zero means nothing at all was received.
    /// </summary>
    public long AdvertisementsSeen { get; set; }
    public string? Adapter { get; set; }
}

/// <summary>
/// The recorded observations of one car plus the figures that answer "is this link healthy".
/// </summary>
public class DtoBleBeaconHistory
{
    public List<DtoBleBeaconObservation> Observations { get; set; } = new();
    public int TotalScans { get; set; }
    public int FoundScans { get; set; }
    /// <summary>Share of polls at which the car counted as present, 0 to 100. Null when nothing was recorded yet.</summary>
    public double? HitRatePercent { get; set; }
    /// <summary>Mean RSSI over the polls at which the car had advertised. Null when it never advertised.</summary>
    public double? AverageRssi { get; set; }
    /// <summary>Longest run of consecutive polls at which the car was not present, which is what suspends charging.</summary>
    public int LongestMissStreak { get; set; }
    public DateTimeOffset? LastFoundAt { get; set; }
}
