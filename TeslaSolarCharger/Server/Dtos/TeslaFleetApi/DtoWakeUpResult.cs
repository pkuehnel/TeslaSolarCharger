using Newtonsoft.Json;

namespace TeslaSolarCharger.Server.Dtos.TeslaFleetApi;

public class DtoWakeUpResult
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("user_id")]
    public int UserId { get; set; }

    [JsonProperty("vehicle_id")]
    public int VehicleId { get; set; }

    [JsonProperty("vin")]
    public string Vin { get; set; }

    [JsonProperty("color")]
    public object Color { get; set; }

    [JsonProperty("access_type")]
    public string AccessType { get; set; }

    [JsonProperty("granular_access")]
    public GranularAccess GranularAccess { get; set; }

    [JsonProperty("tokens")]
    public List<string> Tokens { get; set; }

    [JsonProperty("state")]
    public object State { get; set; }

    [JsonProperty("in_service")]
    public bool InService { get; set; }

    [JsonProperty("id_s")]
    public string IdS { get; set; }

    [JsonProperty("calendar_enabled")]
    public bool CalendarEnabled { get; set; }

    [JsonProperty("api_version")]
    public object ApiVersion { get; set; }

    [JsonProperty("backseat_token")]
    public object BackseatToken { get; set; }

    [JsonProperty("backseat_token_updated_at")]
    public object BackseatTokenUpdatedAt { get; set; }
}

public class GranularAccess
{
    [JsonProperty("hide_private")]
    public bool HidePrivate { get; set; }
}
