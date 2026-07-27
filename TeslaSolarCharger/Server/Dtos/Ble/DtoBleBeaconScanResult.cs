namespace TeslaSolarCharger.Server.Dtos.Ble;

/// <summary>
/// Result of a passive BLE beacon scan executed by the BLE container's tesla-beacon-scan helper (transported as JSON
/// in <see cref="TeslaSolarCharger.Shared.Dtos.Ble.DtoBleCommandResult.ResultMessage"/>). The advertisement counts
/// allow distinguishing an absent car (radio provably received other traffic) from a deaf/starved Bluetooth radio
/// (nothing received at all).
/// </summary>
public class DtoBleBeaconScanResult
{
    /// <summary>
    /// True if the car's BLE advertisement was received during the scan window, proving the car is in BLE range.
    /// </summary>
    public bool BeaconFound { get; set; }

    /// <summary>
    /// Signal strength of the car's advertisement in dBm, only set when the beacon was found.
    /// </summary>
    public int? Rssi { get; set; }

    /// <summary>
    /// Bluetooth address the car's advertisement was received from, only set when the beacon was found.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Whether the car currently accepts BLE connections, only set when the beacon was found. False means the car is
    /// already connected to the maximum number of BLE devices.
    /// </summary>
    public bool? Connectable { get; set; }

    /// <summary>
    /// Number of advertisements received from other devices during the scan window. Zero combined with an unfound
    /// beacon means the radio might be deaf, so the car's absence can not be trusted.
    /// </summary>
    public int OtherAdvertisementsSeen { get; set; }

    /// <summary>
    /// Number of distinct devices the advertisements were received from.
    /// </summary>
    public int DistinctDevicesSeen { get; set; }

    /// <summary>
    /// How long the scan actually ran: a found beacon ends the scan early, an unfound one uses the full window.
    /// </summary>
    public long ScanDurationMs { get; set; }
}
