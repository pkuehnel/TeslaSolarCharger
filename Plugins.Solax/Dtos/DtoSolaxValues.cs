using System.Text.Json.Serialization;

namespace Plugins.Solax.Dtos;

public class DtoSolaxValues
{
    [JsonPropertyName("sn")]
#pragma warning disable CS8618
    public string SerialNumber { get; set; }
#pragma warning restore CS8618

    [JsonPropertyName("ver")]
#pragma warning disable CS8618
    public string Version { get; set; }
#pragma warning restore CS8618

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("Data")]
#pragma warning disable CS8618
    public List<int> Data { get; set; }
#pragma warning restore CS8618

    [JsonPropertyName("Information")]
#pragma warning disable CS8618
    public List<object> Information { get; set; }
#pragma warning restore CS8618
}
