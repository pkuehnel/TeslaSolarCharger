using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;
using TeslaSolarCharger.GridPriceProvider.Data;
using TeslaSolarCharger.GridPriceProvider.Data.Options;
using TeslaSolarCharger.GridPriceProvider.Services.Interfaces;

namespace TeslaSolarCharger.GridPriceProvider.Services;

public class HomeAssistantService : IPriceDataService
{
    private readonly HomeAssistantOptions _options;
    private readonly HttpClient _client;

    public HomeAssistantService(HttpClient client, IOptions<HomeAssistantOptions> options, ILogger<HomeAssistantService> logger)
    {
        _options = options.Value;
        _client = client;
    }

    public async Task<IEnumerable<Price>> GetPriceData(DateTimeOffset from, DateTimeOffset to)
    {
        var url = $"api/history/period/{from.UtcDateTime:o}?end={to.UtcDateTime:o}&filter_entity_id={_options.EntityId}";
        var resp = await _client.GetAsync(url);
        resp.EnsureSuccessStatusCode();
        var homeAssistantResponse = await JsonSerializer.DeserializeAsync<List<List<HomeAssistantResponse>>>(await resp.Content.ReadAsStreamAsync()) ?? throw new Exception("Deserialization of Home Assistant API response failed");
        var history = homeAssistantResponse.SingleOrDefault();
        if (history == null || !history.Any())
        {
            throw new Exception($"No data from Home Assistant for entity id {_options.EntityId}, ensure it.");
        }
        var prices = new List<Price>();
        for (var i = 0; i < history.Count; i++)
        {
            var state = history[i];
            var price = decimal.Parse(state.State);
            var validFrom = state.LastUpdated;
            var validTo = (i < history.Count - 1) ? history[i + 1].LastUpdated : to;

            prices.Add(new Price
            {
                Value = price,
                ValidFrom = validFrom,
                ValidTo = validTo
            });
        }
        return prices;
    }

    public class HomeAssistantResponse
    {
        [JsonPropertyName("entity_id")]
        public string EntityId { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }

        [JsonPropertyName("last_changed")]
        public DateTimeOffset LastChanged { get; set; }

        [JsonPropertyName("last_updated")]
        public DateTimeOffset LastUpdated { get; set; }
    }
}
