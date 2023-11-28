using Newtonsoft.Json;

namespace TeslaSolarCharger.Server.Dtos.TeslaFleetApi;

public class DtoTeslaFleetApiRefreshToken
{
    [JsonProperty("access_token")]
    public string AccessToken { get; set; }

    [JsonProperty("refresh_token")]
    public string RefreshToken { get; set; }

    [JsonProperty("id_token")]
    public string IdToken { get; set; }

    [JsonProperty("expires_in")]
    public int ExpiresIn { get; set; }
}
