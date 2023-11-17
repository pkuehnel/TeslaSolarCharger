//using Microsoft.Extensions.Options;
//using System.Text.Json;
//using System.Text.Json.Serialization;
//using TeslaSolarCharger.GridPriceProvider.Data;
//using TeslaSolarCharger.GridPriceProvider.Data.Options;
//using TeslaSolarCharger.GridPriceProvider.Services.Interfaces;

//namespace TeslaSolarCharger.GridPriceProvider.Services;

//public class EnerginetService : IPriceDataService
//{
//    private readonly HttpClient _client;
//    private readonly EnerginetOptions _options;
//    private readonly FixedPriceService _fixedPriceService;

//    public EnerginetService(HttpClient client, IOptions<EnerginetOptions> options)
//    {
//        _client = client;
//        _options = options.Value;

//        if (_options.FixedPrices != null)
//        {
//            _fixedPriceService = new FixedPriceService(Options.Create(_options.FixedPrices));
//        }
//    }

//    public async Task<IEnumerable<Price>> GetPriceData(DateTimeOffset from, DateTimeOffset to)
//    {
//        var url = "Elspotprices?offset=0&start=" + from.AddHours(-2).AddMinutes(-1).UtcDateTime.ToString("yyyy-MM-ddTHH:mm") + "&end=" + to.AddHours(2).AddMinutes(1).UtcDateTime.ToString("yyyy-MM-ddTHH:mm") + "&filter={\"PriceArea\":[\"" + _options.Region + "\"]}&sort=HourUTC ASC&timezone=dk".Replace(@"\", string.Empty); ;
//        var resp = await _client.GetAsync(url);

//        resp.EnsureSuccessStatusCode();

//        var prices = new List<Price>();
//        var EnerginetResponse = await JsonSerializer.DeserializeAsync<EnerginetResponse>(await resp.Content.ReadAsStreamAsync());

//        if (EnerginetResponse.Records.Count > 0)
//        {
//            foreach (var record in EnerginetResponse.Records)
//            {
//                decimal fixedPrice = 0;
//                if (_fixedPriceService != null)
//                {
//                    var fixedPrices = await _fixedPriceService.GetPriceData(record.HourUTC, record.HourUTC.AddHours(1));
//                    fixedPrice = fixedPrices.Sum(p => p.Value);
//                }

//                var spotPrice = _options.Currency switch
//                {
//                    EnerginetCurrency.DKK => record.SpotPriceDKK,
//                    EnerginetCurrency.EUR => record.SpotPriceEUR,
//                    _ => throw new ArgumentOutOfRangeException(nameof(_options.Currency)),
//                };

//                var price = ((spotPrice / 1000) + fixedPrice);
//                if (_options.VAT.HasValue)
//                {
//                    price *= _options.VAT.Value;
//                }
//                prices.Add(new Price
//                {
//                    ValidFrom = record.HourUTC,
//                    ValidTo = record.HourUTC.AddHours(1),
//                    Value = price
//                });
//            }
//        }

//        return prices;
//    }

//    private class EnerginetResponse
//    {
//        [JsonPropertyName("records")]
//        public List<EnerginetResponseRow> Records { get; set; }
//    }

//    private class EnerginetResponseRow
//    {
//        private DateTime _hourUTC;

//        [JsonPropertyName("HourUTC")]
//        public DateTime HourUTC { get => _hourUTC; set => _hourUTC = DateTime.SpecifyKind(value, DateTimeKind.Utc); }

//        [JsonPropertyName("SpotPriceDKK")]
//        public decimal SpotPriceDKK { get; set; }

//        [JsonPropertyName("SpotPriceEUR")]
//        public decimal SpotPriceEUR { get; set; }
//    }
//}
