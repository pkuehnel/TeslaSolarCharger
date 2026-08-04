using TeslaSolarCharger.BleApi.Dtos;

namespace TeslaSolarCharger.BleApi.InMemoryValues.Contracts;

public interface ISettings
{
    bool BleRequestAllowed { get; set; }
    DateTimeOffset LastBleAllowedRequest { get; set; }
    /// <summary>
    /// Worker flags overridden at runtime, so the scan modes can be compared on real hardware without redeploying the
    /// container. Null means "use the configured value". Applied on the next worker start.
    /// </summary>
    DtoBleScannerOverrides ScannerOverrides { get; }
}