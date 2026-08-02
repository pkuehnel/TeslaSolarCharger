namespace PkSoftwareService.Custom.Backend.Ble;

public enum BleAdapterState
{
    Down = 0,
    Up = 1,
    /// <summary>
    /// Blocked via rfkill (soft or hard).
    /// </summary>
    Blocked = 2,
    /// <summary>
    /// A BLE worker currently holds the exclusive HCI user channel. The kernel reports such a device as down, so this
    /// state replaces <see cref="Down"/> to avoid confusion.
    /// </summary>
    OwnedByWorker = 3,
}
