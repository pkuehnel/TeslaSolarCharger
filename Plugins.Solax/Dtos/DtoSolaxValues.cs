using System.Text.Json.Serialization;

namespace Plugins.Solax.Dtos;

public class DtoSolaxValues
{
    [JsonPropertyName("sn")]
    public string SerialNumber { get; set; }

    [JsonPropertyName("ver")]
    public string Version { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("Data")]
    public List<int> Data { get; set; }

    [JsonPropertyName("Information")]
    public List<object> Information { get; set; }
}
