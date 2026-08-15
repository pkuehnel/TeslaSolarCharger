namespace PkSoftwareService.Custom.Backend.Ble;

public class DtoBleAdapter
{
    /// <summary>
    /// The adapter's BD address in uppercase colon separated form ("AA:BB:CC:DD:EE:FF"). This is the only identifier
    /// stable across reboots and replugs and the value TSC stores per car. Null when the address is not known yet
    /// (an adapter that has never been up since boot reports an all zero address).
    /// </summary>
    public string? StableId { get; set; }
    public int HciIndex { get; set; }
    /// <summary>
    /// Kernel device name, e.g. "hci0". Not stable across reboots.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Bus the adapter is attached to: "usb" for dongles, "uart" for the onboard adapter of a Raspberry Pi. Other
    /// bus types are reported as their raw numeric value.
    /// </summary>
    public string Bus { get; set; } = string.Empty;
    /// <summary>
    /// USB product string when available, e.g. "CSR8510 A10". Null for non-USB adapters.
    /// </summary>
    public string? UsbProduct { get; set; }
    public BleAdapterState State { get; set; }
    public bool AddressKnown { get; set; }
    /// <summary>
    /// True for the adapter a request without an explicit adapter parameter resolves to.
    /// </summary>
    public bool IsDefault { get; set; }
}
