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
        var latestKnownSpotPriceTime = await LatestKnownSpotPriceStartTime().ConfigureAwait(false);
        DateTimeOffset? getPricesFrom = null;
        if (latestKnownSpotPriceTime != default)
        {
            getPricesFrom = latestKnownSpotPriceTime;
        }
        getPricesFrom ??= _constants.FirstChargePriceTimeStamp;
        var getPricesFromDateTime = getPricesFrom.Value.UtcDateTime;
        var getPricesTo = _dateTimeProvider.DateTimeOffSetUtcNow().AddHours(48);
        var getPricesToDateTime = getPricesTo.UtcDateTime;
        var chargePricesAfterGetPricesFrom = await _teslaSolarChargerContext.ChargePrices
            .Where(c => c.ValidSince > getPricesFromDateTime && c.ValidSince <= getPricesToDateTime)
            .AsNoTracking()
            .ToListAsync().ConfigureAwait(false);
        var latestChargePriceBeforeInitialTimeStamp = await _teslaSolarChargerContext.ChargePrices
            .OrderByDescending(c => c.ValidSince)
            .FirstAsync(c => c.ValidSince <= getPricesFromDateTime).ConfigureAwait(false);
        var chargePrices = new List<ChargePrice>() { latestChargePriceBeforeInitialTimeStamp, };
        chargePrices.AddRange(chargePricesAfterGetPricesFrom);
        chargePrices = chargePrices.OrderBy(c => c.ValidSince).ToList();
        for (var i = 0; i < chargePrices.Count; i++)
        {
            var chargePrice = chargePrices[i];
            if (chargePrice.SpotPriceRegion == default)
            {
                _logger.LogInformation("Can not get spot prices for chargePrice {@chargePrice} as Spot price region is unknown", chargePrice);
                continue;
            }
            var validSinceDateTimeOffset = new DateTimeOffset(chargePrice.ValidSince, TimeSpan.Zero);
            var startDate = getPricesFrom.Value > validSinceDateTimeOffset
                ? getPricesFrom.Value
                : validSinceDateTimeOffset;
            var endDate = i == chargePrices.Count - 1
                ? new DateTimeOffset(getPricesToDateTime, TimeSpan.Zero)
                : new DateTimeOffset(chargePrices[i + 1].ValidSince, TimeSpan.Zero);
            var receivedPrices = await GetEnergyChartPrices(startDate, endDate, chargePrice.SpotPriceRegion.Value.ToRegionCode()).ConfigureAwait(false);
            if (receivedPrices == null)
            {
                _logger.LogWarning("Could not get energy chart prices for region {region} between {startDate} and {endDate}",
                    chargePrice.SpotPriceRegion.Value, startDate, endDate);
                continue;
            }
            await AddEnergyChartPricesToDatabase(startDate.AddTicks(1), receivedPrices, chargePrice.SpotPriceRegion.Value);
        }
    }

    private async Task AddEnergyChartPricesToDatabase(DateTimeOffset earlieststartDate, DtoEnergyChartPrices energyChartPrices,
        SpotPriceRegion chargePriceSpotPriceRegion)
    {
        var newSpotPrices = GenerateSpotPricesFromEnergyChartPrices(earlieststartDate, energyChartPrices, chargePriceSpotPriceRegion);

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        context.SpotPrices.AddRange(newSpotPrices);
        await context.SaveChangesAsync().ConfigureAwait(false);
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

    private async Task<DateTimeOffset> LatestKnownSpotPriceStartTime()
    {
        var latestKnownSpotPriceTime = await _teslaSolarChargerContext.SpotPrices
            .Where(s => s.SpotPriceRegion != null)
            .OrderByDescending(s => s.StartDate)
            .Select(s => s.StartDate)
            .LastOrDefaultAsync().ConfigureAwait(false);
        return new DateTimeOffset(latestKnownSpotPriceTime, TimeSpan.Zero);
    }

    internal string GenerateEnergyChartUrl(DateTimeOffset fromDate, DateTimeOffset toDate, string regionCode)
    {
        const string baseUrl = "https://api.energy-charts.info/price";
        const string dateFormat = "yyyy-MM-dd'T'HH:mm'Z'"; // note the closing quote

        var query = new Dictionary<string, string?>
        {
            ["bzn"] = regionCode,
            ["start"] = fromDate.ToUniversalTime().ToString(dateFormat, CultureInfo.InvariantCulture),
            ["end"] = toDate.ToUniversalTime().ToString(dateFormat, CultureInfo.InvariantCulture),
        };

        return QueryHelpers.AddQueryString(baseUrl, query);
    }

    private async Task<DtoEnergyChartPrices?> GetEnergyChartPrices(DateTimeOffset fromDate, DateTimeOffset toDate, string regionCode)
    {
        var url = GenerateEnergyChartUrl(fromDate, toDate, regionCode);
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromHours(_constants.SpotPriceRefreshIntervalHours);
        var json = await httpClient.GetStringAsync(url)
            .ConfigureAwait(false);
        var prices = GetPrices(json);
        return prices;
    }

    internal DtoEnergyChartPrices? GetPrices(string json)
    {
        return JsonConvert.DeserializeObject<DtoEnergyChartPrices>(json);
    }
}
