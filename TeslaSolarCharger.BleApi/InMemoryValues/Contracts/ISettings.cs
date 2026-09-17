namespace TeslaSolarCharger.BleApi.InMemoryValues.Contracts;

public interface ISettings
{
    bool BleRequestAllowed { get; set; }
    DateTimeOffset LastBleAllowedRequest { get; set; }
}
