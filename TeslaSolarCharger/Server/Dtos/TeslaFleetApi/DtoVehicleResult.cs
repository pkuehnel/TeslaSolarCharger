using Newtonsoft.Json;

namespace TeslaSolarCharger.Server.Dtos.TeslaFleetApi;

public class DtoVehicleResult
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("vehicle_id")]
    public long VehicleId { get; set; }

    [JsonProperty("vin")]
    public string Vin { get; set; }

    [JsonProperty("state")]
    public string State { get; set; }
}
