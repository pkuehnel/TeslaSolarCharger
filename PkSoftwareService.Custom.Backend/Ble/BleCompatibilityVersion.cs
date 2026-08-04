namespace PkSoftwareService.Custom.Backend.Ble;

/// <summary>
/// The lockstep compatibility version of the BLE container and TSC. Served by the container's
/// Hello/TscVersionCompatibility endpoint and compared (exact equality) by TSC's version check. Bump it here; both
/// sides compile against this single constant.
/// </summary>
public static class BleCompatibilityVersion
{
    public static readonly Version Value = new(2, 42, 0);
}
