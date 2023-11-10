using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using TeslaSolarCharger.GridPriceProvider.Data;
using TeslaSolarCharger.GridPriceProvider.Services.Interfaces;
using TeslaSolarCharger.Shared.Dtos.ChargingCost.CostConfigurations;

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
        var fixedPricesStrings = JsonConvert.DeserializeObject<List<string>>(configString);
        if (fixedPricesStrings == null)
        {
            throw new ArgumentNullException(nameof(fixedPricesStrings));
        }
        var prices = new List<Price>();
        var started = false;
        var dayIndex = -1;
        int fpIndex = 0;
        var maxIterations = 100; // fail-safe against infinite loop
        var fixedPrices = ParseConfigString(configString);
        for (var i = 0; i <= maxIterations; i++)
        {
            var fixedPrice = fixedPrices[fpIndex];
            var validFrom = DateTime.SpecifyKind(from.Date.AddDays(dayIndex).AddHours(fixedPrice.FromHour).AddMinutes(fixedPrice.FromMinute), DateTimeKind.Utc);
            var validTo = DateTime.SpecifyKind(from.Date.AddDays(dayIndex).AddHours(fixedPrice.ToHour).AddMinutes(fixedPrice.ToMinute), DateTimeKind.Utc);
            var price = new Price
            {
                ValidFrom = validFrom.Add(-TimeZoneInfo.Local.GetUtcOffset(validFrom)),
                ValidTo = validTo.Add(-TimeZoneInfo.Local.GetUtcOffset(validTo)),
                Value = fixedPrice.Value
            };
            if (price.ValidFrom < to && price.ValidTo > from)
            {
                prices.Add(price);
                started = true;
            }
            else if (started)
            {
                break;
            }
            fpIndex++;
            if (fpIndex >= fixedPrices.Count)
            {
                fpIndex = 0;
                dayIndex++;
            }
            if (i == maxIterations)
            {
                throw new Exception("Infinite loop detected within FixedPrice provider");
            }
        }

        return Task.FromResult(prices.AsEnumerable());
    }

    private static readonly Regex FixedPriceRegex = new Regex("(\\d\\d):(\\d\\d)-(\\d\\d):(\\d\\d)=(.+)");

    private List<FixedPrice> GetFixedPrices(List<string> fixedPricesString)
    {
        var fixedPrices = new List<FixedPrice>();
        var totalHours = 0M;
        FixedPrice lastFixedPrice = null;

        foreach (var price in fixedPricesString.OrderBy(x => x))
        {
            var match = FixedPriceRegex.Match(price);
            if (!match.Success) { throw new ArgumentException(nameof(price), $"Failed to parse fixed price: {price}"); }
            var fromHour = int.Parse(match.Groups[1].Value);
            var fromMinute = int.Parse(match.Groups[2].Value);
            var toHour = int.Parse(match.Groups[3].Value);
            var toMinute = int.Parse(match.Groups[4].Value);
            if (!decimal.TryParse(match.Groups[5].Value, out var value))
            {
                throw new ArgumentException(nameof(value), $"Failed to parse fixed price value: {match.Groups[5].Value}");
            }
            var fromHours = fromHour + (fromMinute / 60M);
            var toHours = toHour + (toMinute / 60M);
            if (fromHours > toHours)
            {
                toHours += 24;
                toHour += 24;
            }
            var fixedPrice = new FixedPrice
            {
                FromHour = fromHour,
                FromMinute = fromMinute,
                ToHour = toHour,
                ToMinute = toMinute,
                Value = value
            };
            fixedPrices.Add(fixedPrice);

            if (lastFixedPrice != null && (fixedPrice.FromHour != lastFixedPrice.ToHour || fixedPrice.FromMinute != lastFixedPrice.ToMinute))
            {
                throw new ArgumentException(nameof(price), $"Price from time does not match previous to time: {price}");
            }
            totalHours += toHours - fromHours;
            lastFixedPrice = fixedPrice;
        }
        if (totalHours != 24)
        {
            throw new ArgumentException(nameof(totalHours), $"Total hours do not equal 24, currently {totalHours}");
        }
        return fixedPrices;
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
