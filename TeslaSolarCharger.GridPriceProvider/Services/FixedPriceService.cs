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
