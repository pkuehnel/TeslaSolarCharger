using Newtonsoft.Json;

namespace TeslaSolarCharger.Server.Dtos.TeslaFleetApi;

public class DtoVehicleCommandResult
{
    [JsonProperty("reason")]
    public string Reason { get; set; }

    [JsonProperty("result")]
    public bool Result { get; set; }
}
