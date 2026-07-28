namespace TeslaSolarCharger.Server.Dtos.Ble;

/// <summary>
/// Result of a passive BLE beacon scan of the BLE container (transported as JSON in
/// <see cref="TeslaSolarCharger.Shared.Dtos.Ble.DtoBleCommandResult.ResultMessage"/>). Cars advertise continuously,
/// awake and asleep, so a scan answers whether the car is in range without connecting to it and can never wake it.
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
}
