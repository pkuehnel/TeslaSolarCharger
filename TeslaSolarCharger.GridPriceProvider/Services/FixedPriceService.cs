using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TeslaSolarCharger.GridPriceProvider.Data;
using TeslaSolarCharger.GridPriceProvider.Services.Interfaces;
using TeslaSolarCharger.Shared.Dtos.ChargingCost.CostConfigurations;

[assembly: InternalsVisibleTo("TeslaSolarCharger.Tests")]
namespace TeslaSolarCharger.GridPriceProvider.Services;

public class FixedPriceService : IFixedPriceService
{
    private readonly ILogger<FixedPriceService> _logger;

    public FixedPriceService(ILogger<FixedPriceService> logger)
    {
        _logger = logger;
    }

    public Task<IEnumerable<Price>> GetPriceData(DateTimeOffset from, DateTimeOffset to, string? configString)
    {
        _logger.LogTrace("{method}({from}, {to}, {fixedPriceStrings}", nameof(GetPriceData), from, to, configString);
        if (string.IsNullOrWhiteSpace(configString))
        {
            throw new ArgumentNullException(nameof(configString));
        }

        var fixedPrices = ParseConfigString(configString);
        var prices = GeneratePricesBasedOnFixedPrices(from, to, fixedPrices);


        return Task.FromResult(prices.AsEnumerable());
    }

    internal List<Price> GeneratePricesBasedOnFixedPrices(DateTimeOffset from, DateTimeOffset to, List<FixedPrice> fixedPrices)
    {
        var result = new List<Price>();
        var midnightseparatedFixedPrices = SplitFixedPricesAtMidnight(fixedPrices);
        foreach (var fixedPrice in midnightseparatedFixedPrices)
        {
            var fromLocal = from.ToLocalTime();
            var toLocal = to.ToLocalTime();
            // Check each day in the range
            for (var day = fromLocal.Date; day <= toLocal.Date; day = day.AddDays(1))
            {
                // If ValidOnDays is null, the price is considered valid every day
                if (fixedPrice.ValidOnDays == null || fixedPrice.ValidOnDays.Contains(day.DayOfWeek))
                {
                    var validFrom = new DateTimeOffset(day.AddHours(fixedPrice.FromHour).AddMinutes(fixedPrice.FromMinute)).ToUniversalTime();
                    var validTo = new DateTimeOffset(day.AddHours(fixedPrice.ToHour).AddMinutes(fixedPrice.ToMinute)).ToUniversalTime();

                    if (validTo.TimeOfDay == TimeSpan.Zero)
                    {
                        validTo = validTo.AddDays(1);
                    }

                    result.Add(new Price
                    {
                        Value = fixedPrice.Value,
                        ValidFrom = validFrom,
                        ValidTo = validTo,
                    });
                }
            }
        }
        return result;
    }

    internal List<FixedPrice> SplitFixedPricesAtMidnight(List<FixedPrice> originalPrices)
    {
        var splitPrices = new List<FixedPrice>();

        foreach (var price in originalPrices)
        {
            // If the 'To' time is on or after midnight (next day), and 'From' time is before midnight (same day)
            if (price.ToHour < price.FromHour || (price.ToHour == price.FromHour && price.ToMinute < price.FromMinute))
            {
                // Create a new FixedPrice for the period before midnight
                var priceBeforeMidnight = new FixedPrice
                {
                    FromHour = price.FromHour,
                    FromMinute = price.FromMinute,
                    ToHour = 0, // Set to last hour of the day
                    ToMinute = 0, // Set to last minute of the day
                    Value = price.Value,
                    ValidOnDays = price.ValidOnDays,
                };

                // Create a new FixedPrice for the period after midnight
                var priceAfterMidnight = new FixedPrice
                {
                    FromHour = 0,
                    FromMinute = 0,
                    ToHour = price.ToHour,
                    ToMinute = price.ToMinute,
                    Value = price.Value,
                    ValidOnDays = price.ValidOnDays,
                };

                splitPrices.Add(priceBeforeMidnight);
                splitPrices.Add(priceAfterMidnight);
            }
            else
            {
                // If the price does not span over midnight, just add it to the list
                splitPrices.Add(price);
            }
        }

        return splitPrices;
    }


    public string GenerateConfigString(List<FixedPrice> prices)
    {
        var json = JsonConvert.SerializeObject(prices);
        return json;
    }

    public List<FixedPrice> ParseConfigString(string configString)
    {
        var fixedPrices = JsonConvert.DeserializeObject<List<FixedPrice>>(configString);
        if (fixedPrices == null)
        {
            throw new ArgumentNullException(nameof(fixedPrices));
        }
        return fixedPrices;
    }
}
