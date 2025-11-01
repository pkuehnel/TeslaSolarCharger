using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Globalization;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos.EnergyCharts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class SpotPriceService : ISpotPriceService
{
    private readonly ILogger<SpotPriceService> _logger;
    private readonly ITeslaSolarChargerContext _teslaSolarChargerContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IConstants _constants;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public SpotPriceService(ILogger<SpotPriceService> logger, ITeslaSolarChargerContext teslaSolarChargerContext,
        IDateTimeProvider dateTimeProvider, IConstants constants,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _teslaSolarChargerContext = teslaSolarChargerContext;
        _dateTimeProvider = dateTimeProvider;
        _constants = constants;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task UpdateSpotPrices()
    {
        _logger.LogTrace("{method}()", nameof(UpdateSpotPrices));
        var deletedRows = await _teslaSolarChargerContext.SpotPrices
            .Where(s => s.SpotPriceRegion == null)
            .ExecuteDeleteAsync();
        if (deletedRows > 0)
        {
            _logger.LogInformation("Deleted {deletedRows} spot prices from database.", deletedRows);
        }

        var regions = await _teslaSolarChargerContext.ChargePrices
            .Where(c => c.AddSpotPriceToGridPrice && c.SpotPriceRegion != null)
            .Select(c => c.SpotPriceRegion)
            .ToHashSetAsync();
        var latestKnownSpotPriceTimes = await LatestKnownSpotPriceStartTime(regions).ConfigureAwait(false);
        foreach (var latestKnownSpotPriceTime in latestKnownSpotPriceTimes)
        {
            DateTimeOffset? getPricesFrom = null;
            if (latestKnownSpotPriceTime.Value != default)
            {
                getPricesFrom = latestKnownSpotPriceTime.Value;
            }
            getPricesFrom ??= _constants.FirstChargePriceTimeStamp;
            var getPricesTo = _dateTimeProvider.DateTimeOffSetUtcNow().AddHours(48);
            var receivedPrices = await GetEnergyChartPrices(getPricesFrom.Value, getPricesTo, latestKnownSpotPriceTime.Key.ToRegionCode()).ConfigureAwait(false);
            if (receivedPrices == null)
            {
                _logger.LogWarning("Could not get energy chart prices for region {region} between {startDate} and {endDate}",
                    latestKnownSpotPriceTime.Key, getPricesFrom.Value, getPricesTo);
                continue;
            }
            await AddEnergyChartPricesToDatabase(getPricesFrom.Value.AddTicks(1), receivedPrices, latestKnownSpotPriceTime.Key);
        }
        
    }

    private async Task AddEnergyChartPricesToDatabase(DateTimeOffset earlieststartDate, DtoEnergyChartPrices energyChartPrices,
        SpotPriceRegion chargePriceSpotPriceRegion)
    {
        var newSpotPrices = GenerateSpotPricesFromEnergyChartPrices(earlieststartDate, energyChartPrices, chargePriceSpotPriceRegion);

        //Create batches as otherwise old raspis might crash
        foreach (var batch in newSpotPrices.Chunk(1000))
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
            context.SpotPrices.AddRange(batch);
            await context.SaveChangesAsync().ConfigureAwait(false);
        }
    }

    internal List<SpotPrice> GenerateSpotPricesFromEnergyChartPrices(DateTimeOffset earlieststartDate,
        DtoEnergyChartPrices energyChartPrices, SpotPriceRegion chargePriceSpotPriceRegion)
    {
        var newSpotPrices = new List<SpotPrice>();
        for (var i = 0; i < energyChartPrices.unix_seconds.Count; i++)
        {
            var startDate = DateTimeOffset.FromUnixTimeSeconds(energyChartPrices.unix_seconds[i]);
            if (startDate < earlieststartDate)
            {
                continue;
            }
            newSpotPrices.Add(new()
            {
                StartDate = startDate.UtcDateTime,
                Price = energyChartPrices.price[i] / 1000,
                SpotPriceRegion = chargePriceSpotPriceRegion,
            });
        }
        return newSpotPrices;
    }

    private async Task<Dictionary<SpotPriceRegion, DateTimeOffset?>> LatestKnownSpotPriceStartTime(HashSet<SpotPriceRegion?> regions)
    {
        regions.Remove(null);
        
        var result = new Dictionary<SpotPriceRegion, DateTimeOffset?>();
        foreach (var spotPriceRegion in regions)
        {
            if (spotPriceRegion == default)
            {
                continue;
            }

            var lateststartDate = await _teslaSolarChargerContext.SpotPrices
                .Where(s => s.SpotPriceRegion == spotPriceRegion)
                .OrderByDescending(s => s.StartDate)
                .Select(s => new { s.StartDate })
                .FirstOrDefaultAsync().ConfigureAwait(false);
            result[spotPriceRegion.Value] = lateststartDate == default ? null : new DateTimeOffset(lateststartDate.StartDate, TimeSpan.Zero);
        }
        return result;
    }

    internal string GenerateEnergyChartUrl(DateTimeOffset fromDate, DateTimeOffset toDate, string regionCode)
    {
        const string baseUrl = "https://api.energy-charts.info/price";
        const string dateFormat = "yyyy-MM-dd'T'HH:mm'Z'"; // note the closing quote
        var fromDateString = fromDate.ToUniversalTime().ToString(dateFormat, CultureInfo.InvariantCulture);
        var toDateString = toDate.ToUniversalTime().ToString(dateFormat, CultureInfo.InvariantCulture);
        var query = new Dictionary<string, string?>
        {
            ["bzn"] = regionCode,
            ["start"] = fromDateString,
            ["end"] = toDateString,
        };

        return QueryHelpers.AddQueryString(baseUrl, query);
    }

    private async Task<DtoEnergyChartPrices?> GetEnergyChartPrices(DateTimeOffset fromDate, DateTimeOffset toDate, string regionCode)
    {
        var url = GenerateEnergyChartUrl(fromDate, toDate, regionCode);
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromHours(_constants.SpotPriceRefreshIntervalHours);
        var response = await httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var prices = GetPrices(json);
        return prices;
    }

    internal DtoEnergyChartPrices? GetPrices(string json)
    {
        return JsonConvert.DeserializeObject<DtoEnergyChartPrices>(json);
    }
}
