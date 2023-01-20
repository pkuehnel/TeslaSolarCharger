using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos.Awattar;

namespace TeslaSolarCharger.Server.Services;

public class SpotPriceService : ISpotPriceService
{
    private readonly ILogger<SpotPriceService> _logger;
    private readonly ITeslaSolarChargerContext _teslaSolarChargerContext;

    public SpotPriceService(ILogger<SpotPriceService> logger, ITeslaSolarChargerContext teslaSolarChargerContext)
    {
        _logger = logger;
        _teslaSolarChargerContext = teslaSolarChargerContext;
    }

    public async Task UpdateSpotPrices()
    {
        _logger.LogTrace("{method}()", nameof(UpdateSpotPrices));

        var latestKnownSpotPrice = await _teslaSolarChargerContext.SpotPrices
            .OrderBy(s => s.StartDate)
            .LastOrDefaultAsync().ConfigureAwait(false);
        DateTimeOffset? getPricesFrom = null;
        if (latestKnownSpotPrice != default)
        {
            getPricesFrom = latestKnownSpotPrice.EndDate;
        }

        var awattarPrices = await GetAwattarPrices(getPricesFrom).ConfigureAwait(false);
        if (awattarPrices == null)
        {
            _logger.LogWarning("Clould not get awattar prices");
            return;
        }
        var values = awattarPrices.data.OrderBy(d => d.start_timestamp).ToList();

        var newSpotPrices = new List<SpotPrice>();
        foreach (var value in values)
        {
            newSpotPrices.Add(GenerateSpotPriceFromAwattarPrice(value));
        }
        _teslaSolarChargerContext.SpotPrices.AddRange(newSpotPrices);
        await _teslaSolarChargerContext.SaveChangesAsync().ConfigureAwait(false);
    }

    internal SpotPrice GenerateSpotPriceFromAwattarPrice(Datum value)
    {
        var spotPrice = new SpotPrice()
        {
            Price = value.marketprice / 1000,
            StartDate = DateTimeOffset.FromUnixTimeMilliseconds(value.start_timestamp).UtcDateTime,
            EndDate = DateTimeOffset.FromUnixTimeMilliseconds(value.end_timestamp).UtcDateTime,
        };
        return spotPrice;
    }

    internal string GenerateAwattarUrl(DateTimeOffset? fromDate)
    {
        var url = "https://api.awattar.de/v1/marketdata";
        if (fromDate != null)
        {
            url += $"?start={fromDate.Value.ToUnixTimeMilliseconds()}&end={fromDate.Value.AddHours(48).ToUnixTimeMilliseconds()}";
        }
        return url;
    }

    private async Task<DtoAwattarPrices?> GetAwattarPrices(DateTimeOffset? fromDate)
    {
        var url = GenerateAwattarUrl(fromDate);
        using var httpClient = new HttpClient();
        var awattarPrices = await httpClient.GetFromJsonAsync<DtoAwattarPrices>(url)
            .ConfigureAwait(false);
        return awattarPrices;
    }
}
