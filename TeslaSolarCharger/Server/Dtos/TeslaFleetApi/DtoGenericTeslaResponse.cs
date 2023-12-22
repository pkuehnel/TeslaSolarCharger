using Newtonsoft.Json;

namespace TeslaSolarCharger.Server.Dtos.TeslaFleetApi;

public class DtoGenericTeslaResponse <T> where T : class
{
    [JsonProperty("response")]
    public T? Response { get; set; }
    [JsonProperty("error")]
    public string? Error { get; set; }
    [JsonProperty("error_description")]
    public string? ErrorDescription { get; set; }
}
