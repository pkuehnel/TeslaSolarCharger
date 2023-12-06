using Newtonsoft.Json;

namespace TeslaSolarCharger.Server.Dtos.TeslaFleetApi;

public class DtoGenericTeslaResponse <T> where T : class
{
    [JsonProperty("response")]
    public T Response { get; set; }
}
