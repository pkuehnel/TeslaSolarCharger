namespace PkSoftwareService.Custom.Backend.Ble;

public static class BleConstants
{
    /// <summary>
    /// Value TSC sends as keepWarmSeconds on scheduled beacon scan calls so the worker of the polled adapter stays
    /// warm between polls. One-off commands never send the parameter.
    /// </summary>
    public const int BleKeepWarmSeconds = 600;
}
