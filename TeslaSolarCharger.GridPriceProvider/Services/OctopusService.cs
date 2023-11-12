using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;
using TeslaSolarCharger.GridPriceProvider.Data;
using TeslaSolarCharger.GridPriceProvider.Data.Options;
using TeslaSolarCharger.GridPriceProvider.Services.Interfaces;

namespace TeslaSolarCharger.GridPriceProvider.Services;

public class OctopusService : IPriceDataService
{
    private readonly OctopusOptions _options;
    private readonly HttpClient _client;

    public OctopusService(HttpClient client, IOptions<OctopusOptions> options)
    {
        _options = options.Value;
        _client = client;
    }

    public async Task<IEnumerable<Price>> GetPriceData(DateTimeOffset from, DateTimeOffset to)
    {
        var url = $"products/{_options.ProductCode}/electricity-tariffs/{_options.TariffCode}-{_options.RegionCode}/standard-unit-rates?period_from={from.UtcDateTime:o}&period_to={to.UtcDateTime:o}";
        var list = new List<AgilePrice>();
        do
        {
            var resp = await _client.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            var agileResponse = await JsonSerializer.DeserializeAsync<AgileResponse>(await resp.Content.ReadAsStreamAsync()) ?? throw new Exception($"Deserialization of Octopus Agile API response failed");
            list.AddRange(agileResponse.Results);
            url = agileResponse.Next;
            if (string.IsNullOrEmpty(url))
            {
                break;
            }
            else
            {
                Thread.Sleep(5000); // back off API so they don't ban us
            }
        }
        while (true);
        return list
            .Select(x => new Price
            {
                Value = x.ValueIncVAT / 100,
                ValidFrom = x.ValidFrom,
                ValidTo = x.ValidTo
            });
    }

    public class AgilePrice
    {
        [JsonPropertyName("value_exc_vat")]
        public decimal ValueExcVAT { get; set; }

        [JsonPropertyName("value_inc_vat")]
        public decimal ValueIncVAT { get; set; }

        [JsonPropertyName("valid_from")]
        public DateTimeOffset ValidFrom { get; set; }

        [JsonPropertyName("valid_to")]
        public DateTimeOffset ValidTo { get; set; }
    }

    public class AgileResponse
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("next")]
        public string Next { get; set; }

        [JsonPropertyName("previous")]
        public string Previous { get; set; }

        [JsonPropertyName("results")]
        public List<AgilePrice> Results { get; set; }
    }

    public Task<IEnumerable<Price>> GetPriceData(DateTimeOffset from, DateTimeOffset to, string? configString)
    {
        throw new NotImplementedException();
    }
}
