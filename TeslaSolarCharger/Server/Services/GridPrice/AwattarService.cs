using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;
using TeslaSolarCharger.Server.Services.GridPrice.Contracts;
using TeslaSolarCharger.Server.Services.GridPrice.Dtos;
using TeslaSolarCharger.Server.Services.GridPrice.Options;

namespace TeslaSolarCharger.Server.Services.GridPrice;

public class AwattarService : IPriceDataService
{
    private readonly HttpClient _client;
    private readonly AwattarOptions _options;

    public AwattarService(HttpClient client, IOptions<AwattarOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public Task<IEnumerable<Price>> GetPriceData(DateTimeOffset from, DateTimeOffset to, string? configString)
    {
        throw new NotImplementedException();
    }

    public async Task<IEnumerable<Price>> GetPriceData(DateTimeOffset from, DateTimeOffset to)
    {
        var url = $"marketdata?start={from.UtcDateTime.AddHours(-1):o}&end={to.UtcDateTime.AddHours(1):o}";
        var resp = await _client.GetAsync(url);
        resp.EnsureSuccessStatusCode();
        var agileResponse = await JsonSerializer.DeserializeAsync<AwattarResponse>(await resp.Content.ReadAsStreamAsync());
        if (agileResponse == null)
        {
            throw new Exception($"Deserialization of aWATTar API response failed");
        }
        if (agileResponse.Results.Any(x => x.Unit != "Eur/MWh"))
        {
            throw new Exception($"Unknown price unit(s) detected from aWATTar API: {string.Join(", ", agileResponse.Results.Select(x => x.Unit).Distinct())}");
        }
        return agileResponse.Results.Select(x => new Price
        {
            Value = (x.MarketPrice / 1000) * _options.VATMultiplier,
            ValidFrom = DateTimeOffset.FromUnixTimeSeconds(x.StartTimestamp / 1000),
            ValidTo = DateTimeOffset.FromUnixTimeSeconds(x.EndTimestamp / 1000)
        });
    }

    public class AwattarPrice
    {
        [JsonPropertyName("marketprice")]
        public decimal MarketPrice { get; set; }

        [JsonPropertyName("unit")]
        public string Unit { get; set; }

        [JsonPropertyName("start_timestamp")]
        public long StartTimestamp { get; set; }

        [JsonPropertyName("end_timestamp")]
        public long EndTimestamp { get; set; }
    }

    public class AwattarResponse
    {
        [JsonPropertyName("data")]
        public List<AwattarPrice> Results { get; set; }
    }
}
