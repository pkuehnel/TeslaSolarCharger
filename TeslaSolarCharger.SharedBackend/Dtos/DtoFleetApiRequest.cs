using TeslaSolarCharger.SharedBackend.Enums;

namespace TeslaSolarCharger.SharedBackend.Dtos;

public class DtoFleetApiRequest
{
    public string RequestUrl { get; set; }
    public bool NeedsProxy { get; set; }
    public bool BleCompatible { get; set; }
    public TeslaApiRequestType TeslaApiRequestType { get; set; }
}
