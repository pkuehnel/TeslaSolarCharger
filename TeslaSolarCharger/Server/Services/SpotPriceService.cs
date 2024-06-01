using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Server.Dtos.Awattar;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class SpotPriceService : ISpotPriceService
{
    private readonly ILogger<SpotPriceService> _logger;
    private readonly ITeslaSolarChargerContext _teslaSolarChargerContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IConfigurationWrapper _configurationWrapper;

    public SpotPriceService(ILogger<SpotPriceService> logger, ITeslaSolarChargerContext teslaSolarChargerContext,
        IDateTimeProvider dateTimeProvider, IConfigurationWrapper configurationWrapper)
    {
        _logger = logger;
        _teslaSolarChargerContext = teslaSolarChargerContext;
        _dateTimeProvider = dateTimeProvider;
        _configurationWrapper = configurationWrapper;
    }

    public async Task UpdateSpotPrices()
    {
        _logger.LogTrace("{method}()", nameof(UpdateSpotPrices));

        var latestKnownSpotPriceTime = await LatestKnownSpotPriceTime().ConfigureAwait(false);
        DateTimeOffset? getPricesFrom = null;
        if (latestKnownSpotPriceTime != default)
        {
            getPricesFrom = latestKnownSpotPriceTime;
        }

        var awattarPrices = await GetAwattarPrices(getPricesFrom, null).ConfigureAwait(false);
        await AddAwattarPricesToDatabase(awattarPrices);
    }

    public async Task GetSpotPricesSinceFirstChargeDetail()
    {
        _logger.LogTrace("{method}()", nameof(GetSpotPricesSinceFirstChargeDetail));

        var firstChargeDetail = await _teslaSolarChargerContext.ChargingDetails
            .OrderBy(cd => cd.TimeStamp)
            .Select(cd => cd.TimeStamp)
            .FirstOrDefaultAsync().ConfigureAwait(false);
        if (firstChargeDetail == default)
        {
            _logger.LogInformation("No chargingdetails found so gettings spotprices is not needed");
            return;
        }
        var firstSpotPrice = await _teslaSolarChargerContext.SpotPrices
            .OrderBy(sp => sp.StartDate)
            .Select(sp => sp.StartDate)
            .FirstOrDefaultAsync().ConfigureAwait(false);
        if (firstSpotPrice != default && firstSpotPrice < firstChargeDetail)
        {
            _logger.LogInformation("Spotprices already exist for all chargingdetails");
            return;
        }
        var getPricesFrom = new DateTimeOffset(firstChargeDetail.AddDays(-1), TimeSpan.Zero);
        var getPricesTo = new DateTimeOffset(firstSpotPrice == default ? _dateTimeProvider.UtcNow() : firstSpotPrice, TimeSpan.Zero);
        var awattarPrices = await GetAwattarPrices(getPricesFrom, getPricesTo).ConfigureAwait(false);
        await AddAwattarPricesToDatabase(awattarPrices);
    }

    private async Task AddAwattarPricesToDatabase(DtoAwattarPrices? awattarPrices)
    {
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

    public async Task<DateTimeOffset> LatestKnownSpotPriceTime()
    {
        var latestKnownSpotPriceTime = await _teslaSolarChargerContext.SpotPrices
            .OrderBy(s => s.StartDate)
            .Select(s => s.EndDate)
            .LastOrDefaultAsync().ConfigureAwait(false);
        return new DateTimeOffset(latestKnownSpotPriceTime, TimeSpan.Zero);
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

    internal string GenerateAwattarUrl(DateTimeOffset? fromDate, DateTimeOffset? toDate)
    {
        var url = _configurationWrapper.GetAwattarBaseUrl();
        //var url = "https://api.awattar.de/v1/marketdata";
        if (fromDate != null)
        {
            if (toDate == null)
            {
                toDate = _dateTimeProvider.DateTimeOffSetNow().AddHours(48);
            }
            url += $"?start={fromDate.Value.ToUnixTimeMilliseconds()}&end={toDate.Value.ToUnixTimeMilliseconds()}";
        }
        return url;
    }

    private async Task<DtoAwattarPrices?> GetAwattarPrices(DateTimeOffset? fromDate, DateTimeOffset? toDate)
    {
        var url = GenerateAwattarUrl(fromDate, toDate);
        using var httpClient = new HttpClient();
        var awattarPrices = await httpClient.GetFromJsonAsync<DtoAwattarPrices>(url)
            .ConfigureAwait(false);
        return awattarPrices;
    }
}
