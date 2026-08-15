using TeslaSolarCharger.BleApi.Dtos;
using TeslaSolarCharger.BleApi.InMemoryValues.Contracts;

namespace TeslaSolarCharger.BleApi.InMemoryValues;

public class Settings : ISettings
{
    public bool BleRequestAllowed { get; set; }
    public DateTimeOffset LastBleAllowedRequest { get; set; }
}