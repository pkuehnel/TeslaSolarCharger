using Microsoft.Extensions.Caching.Memory;
using TeslaSolarCharger.Server.Scheduling;

namespace TeslaSolarCharger.Server.Services;

public class EnergyDataPredictionCache(ILogger<EnergyDataPredictionCache> logger,
     JobManager jobManager,
     IMemoryCache memoryCache)
{
    private readonly string _solarPredictionPrefix = "SolarPrediction_";
    private readonly string _housePredictionPrefix = "HousePrediction_";




    private async Task CacheSolarPrediction(DateOnly date, int hour, int value)
    {
        logger.LogTrace("{method}({date}, {hour}, {value})", nameof(CacheSolarPrediction), date, hour, value);
        await CachePrediction(_solarPredictionPrefix, date, hour, value);
    }

    private async Task CacheHousePrediction(DateOnly date, int hour, int value)
    {
        logger.LogTrace("{method}({date}, {hour}, {value})", nameof(CacheHousePrediction), date, hour, value);
        await CachePrediction(_housePredictionPrefix, date, hour, value);
    }

    private async Task CachePrediction(string prefix, DateOnly date, int hour, int value)
    {
        logger.LogTrace("{method}({prefix}, {date}, {hour}, {value})", nameof(CachePrediction), prefix, date, hour, value);
        var key = $"{prefix}{date:yyyyMMdd}_{hour}";
        var options = await GetCacheEntryOptions();
        memoryCache.Set(key, value, options);
    }

    private async Task<MemoryCacheEntryOptions> GetCacheEntryOptions()
    {
        logger.LogTrace("{method}()", nameof(GetCacheEntryOptions));
        var nextFireTime = await jobManager.GetWeatherDataRefreshNextFireTimeAsync();
        var estimatedMaxRefreshDuration = TimeSpan.FromMinutes(10);
        return new()
        {
            AbsoluteExpiration = (nextFireTime + estimatedMaxRefreshDuration),
        };
    }
}
