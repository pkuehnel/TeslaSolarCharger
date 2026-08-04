namespace TeslaSolarCharger.BleApi.Dtos;

/// <summary>
/// Worker flags set at runtime instead of by configuration. They exist so the scan modes can be compared on real
/// hardware in one sitting: every change stops the worker, and the next request starts it with the new flags.
/// Not persisted - a container restart falls back to the configured values.
/// </summary>
public class DtoBleScannerOverrides
{
    public bool? PresenceScanEnabled { get; set; }
    public bool? ScanWhileConnected { get; set; }
    public int? PresenceMaxAgeSeconds { get; set; }
    public int? ScanRestartAfterSeconds { get; set; }
    public int? AddressBindingTtlSeconds { get; set; }
}
