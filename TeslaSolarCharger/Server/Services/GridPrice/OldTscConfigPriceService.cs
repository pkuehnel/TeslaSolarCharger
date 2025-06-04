using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Server.Services.GridPrice.Contracts;
using TeslaSolarCharger.Server.Services.GridPrice.Dtos;

namespace TeslaSolarCharger.Server.Services.GridPrice;

public class OldTscConfigPriceService (ILogger<OldTscConfigPriceService> logger,
    ITeslaSolarChargerContext teslaSolarChargerContext) : IOldTscConfigPriceService
{
    public async Task<IEnumerable<Price>> GetPriceData(DateTimeOffset from, DateTimeOffset to, string? configString)
    {
        logger.LogTrace("{method}({from}, {to}, {configString})", nameof(GetPriceData), from, to, configString);
        if (!int.TryParse(configString, out var id))
        {
            logger.LogError("Invalid configString: {configString}", configString);
            throw new ArgumentException("Invalid configString", nameof(configString));
        }
        var price = await teslaSolarChargerContext.ChargePrices.FirstAsync(p => p.Id == id);
        if (!price.AddSpotPriceToGridPrice)
        {
            return new List<Price>()
            {
                new()
                {
                    ValidFrom = from,
                    ValidTo = to, GridPrice = price.GridPrice,
                    SolarPrice = price.SolarPrice,
                },
            };
        }

        var fromDateTime = from.UtcDateTime;
        var toDateTime = to.UtcDateTime;
        var spotPrices = await teslaSolarChargerContext.SpotPrices
            .Where(p => p.EndDate >= fromDateTime && p.StartDate <= toDateTime)
            .OrderBy(p => p.StartDate)
            .ToListAsync();
        var result = new List<Price>();
        foreach (var spotPrice in spotPrices)
        {
            var gridPriceDuringThisSpotPrice = price.GridPrice + spotPrice.Price + spotPrice.Price * price.SpotPriceCorrectionFactor;
            result.Add(new Price
            {
                ValidFrom = new DateTimeOffset(spotPrice.StartDate, TimeSpan.Zero),
                ValidTo = new DateTimeOffset(spotPrice.EndDate, TimeSpan.Zero),
                GridPrice = gridPriceDuringThisSpotPrice,
                SolarPrice = price.SolarPrice,
            });
        }
        return result;
    }
}
