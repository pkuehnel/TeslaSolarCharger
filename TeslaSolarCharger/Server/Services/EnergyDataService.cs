using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class EnergyDataService(ILogger<EnergyDataService> logger,
    ITeslaSolarChargerContext context,
    IMemoryCache memoryCache,
    IConstants constants,
    IDateTimeProvider dateTimeProvider,
    IServiceProvider serviceProvider,
    IConfigurationWrapper configurationWrapper) : IEnergyDataService
{

    public async Task RefreshCachedValues(CancellationToken contextCancellationToken)
    {
        logger.LogTrace("{method}()", nameof(RefreshCachedValues));
        var cacheInPastDays = 10;
        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
        var localDateOnly = DateOnly.FromDateTime(currentDate.LocalDateTime);
        var stopWatch = new Stopwatch();
        stopWatch.Start();
        for (var i = -cacheInPastDays; i <= constants.WeatherPredictionInFutureDays; i++)
        {
            var useCache = i < (-1);
            var date = localDateOnly.AddDays(i);
            await GetPredictedSolarProductionByLocalHour(date, contextCancellationToken, useCache).ConfigureAwait(false);
            await GetPredictedHouseConsumptionByLocalHour(date, contextCancellationToken, useCache).ConfigureAwait(false);
            await GetActualSolarProductionByLocalHour(date, contextCancellationToken, useCache).ConfigureAwait(false);
            await GetActualHouseConsumptionByLocalHour(date, contextCancellationToken, useCache).ConfigureAwait(false);
        }
        stopWatch.Stop();
        logger.LogInformation("Cache refresh took {elapsed}", stopWatch.Elapsed);
    }

    public async Task<Dictionary<int, int>> GetPredictedSolarProductionByLocalHour(DateOnly date, CancellationToken cancellationToken, bool useCache)
    {
        logger.LogTrace("{method}({date}, {useCache})", nameof(GetPredictedSolarProductionByLocalHour), date, useCache);
        if (configurationWrapper.UseFakeEnergyPredictions())
        {
            var fakedResult = GenerateFakeResult();
            return fakedResult;
        }
        if (useCache)
        {
            var cachedResult = GetCachedValues(MeterValueKind.SolarGeneration, true, date);
            if (cachedResult != default)
            {
                return cachedResult;
            }
        }

        var (utcPredictionStart, utcPredictionEnd, historicPredictionsSearchStart) = ComputePredictionTimes(date);
        var hourlyTimeStamps = GetHourlyTimestamps(historicPredictionsSearchStart, utcPredictionStart);

        // Pass cancellationToken to your helper methods
        var createdWh = await GetMeterEnergyDifferencesAsync(hourlyTimeStamps, MeterValueKind.SolarGeneration, cancellationToken);
        var latestRadiations = await GetLatestSolarRadiationsAsync(historicPredictionsSearchStart, utcPredictionEnd, cancellationToken);
        var avgHourlyWeightedFactors = ComputeWeightedAverageFactors(hourlyTimeStamps, createdWh, latestRadiations, historicPredictionsSearchStart);
        var forecastSolarRadiations = await GetForecastSolarRadiationsAsync(utcPredictionStart, utcPredictionEnd, cancellationToken);

        var predictedProduction = ComputePredictedProduction(forecastSolarRadiations, avgHourlyWeightedFactors);
        var result = predictedProduction.OrderBy(x => x.Key).ToDictionary(x => x.Key, y => y.Value);
        CacheValues(MeterValueKind.SolarGeneration, true, date, result);
        return result;
    }

    public async Task<Dictionary<int, int>> GetPredictedHouseConsumptionByLocalHour(DateOnly date,
        CancellationToken httpContextRequestAborted, bool useCache)
    {
        logger.LogTrace("{method}({date}, {useCache})", nameof(GetPredictedHouseConsumptionByLocalHour), date, useCache);
        if (configurationWrapper.UseFakeEnergyPredictions())
        {
            var fakedResult = GenerateFakeResult();
            return fakedResult;
        }
        if (useCache)
        {
            var cachedResult = GetCachedValues(MeterValueKind.HouseConsumption, true, date);
            if (cachedResult != default)
            {
                return cachedResult;
            }
        }

        var (utcPredictionStart, _, historicPredictionsSearchStart) = ComputePredictionTimes(date);
        var hourlyTimeStamps = GetHourlyTimestamps(historicPredictionsSearchStart, utcPredictionStart);
        var createdWh = await GetMeterEnergyDifferencesAsync(hourlyTimeStamps, MeterValueKind.HouseConsumption, httpContextRequestAborted);
        var result = ComputeWeightedMeterValueChanges(hourlyTimeStamps, createdWh, historicPredictionsSearchStart);
        CacheValues(MeterValueKind.HouseConsumption, true, date, result);
        return result.OrderBy(x => x.Key).ToDictionary(x => x.Key, y => y.Value);
    }

    private static Dictionary<int, int> GenerateFakeResult()
    {
        var fakedResult = new Dictionary<int, int>();
        for (var i = 0; i < 24; i++)
        {
            fakedResult[i] = 1000;
        }

        return fakedResult;
    }

    public async Task<Dictionary<int, int>> GetActualSolarProductionByLocalHour(DateOnly date, CancellationToken httpContextRequestAborted, bool useCache)
    {
        logger.LogTrace("{method}({date}, {useCache})", nameof(GetActualSolarProductionByLocalHour), date, useCache);
        if (configurationWrapper.UseFakeEnergyHistory())
        {
            return GenerateFakeResult();
        }
        return await GetActualValuesByLocalHour(MeterValueKind.SolarGeneration, date, httpContextRequestAborted, useCache);
    }

    public async Task<Dictionary<int, int>> GetActualHouseConsumptionByLocalHour(DateOnly date, CancellationToken httpContextRequestAborted, bool useCache)
    {
        logger.LogTrace("{method}({date})", nameof(GetActualHouseConsumptionByLocalHour), date);
        if (configurationWrapper.UseFakeEnergyHistory())
        {
            return GenerateFakeResult();
        }
        return await GetActualValuesByLocalHour(MeterValueKind.HouseConsumption, date, httpContextRequestAborted, useCache);
    }

    private async Task<Dictionary<int, int>> GetActualValuesByLocalHour(MeterValueKind meterValueKind, DateOnly date,
        CancellationToken httpContextRequestAborted, bool useCache)
    {
        var (utcPredictionStart, utcPredictionEnd, _) = ComputePredictionTimes(date);
        var resultHours = GetHourlyTimestamps(utcPredictionStart, utcPredictionEnd);
        var hoursToGetEnergyMeterDifferencesFrom = resultHours.ToList();
        var dateTimeOffsetDictionary = new Dictionary<DateTimeOffset, int>();
        if (useCache)
        {
            foreach (var hourlyTimeStamp in resultHours)
            {
                var hour = hourlyTimeStamp.ToLocalTime().Hour;
                var value = GetCachedValue(meterValueKind, false, date, hour);
                if (value != default)
                {
                    dateTimeOffsetDictionary[hourlyTimeStamp] = value.Value;
                    hoursToGetEnergyMeterDifferencesFrom.Remove(hourlyTimeStamp);
                }
            }
        }
        var dateTimeOffsetDictionaryFromDatabase = await GetMeterEnergyDifferencesAsync(hoursToGetEnergyMeterDifferencesFrom, meterValueKind, httpContextRequestAborted);
        foreach (var databaseValue in dateTimeOffsetDictionaryFromDatabase)
        {
            dateTimeOffsetDictionary[databaseValue.Key] = databaseValue.Value;
        }
        var maxCacheDate = GetMaxCacheDate();
        foreach (var dateTimeOffsetValue in dateTimeOffsetDictionary)
        {
            if (maxCacheDate >= dateTimeOffsetValue.Key)
            {
                CacheValue(meterValueKind, false, date, dateTimeOffsetValue.Key.LocalDateTime.Hour, dateTimeOffsetValue.Value);
            }
        }
        var result = CreateHourlyDictionary(dateTimeOffsetDictionary);
        return result.OrderBy(x => x.Key).ToDictionary(x => x.Key, y => y.Value);
    }

    private DateTimeOffset GetMaxCacheDate()
    {
        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
        //reduce by one hour as dateTimeOffsetDictionary key is one hour older as last relevant value within that hour
        //reduce by twice the save intervals to make sure values are only cached after they have been saved to the database
        var maxCacheDate = currentDate.AddHours(-1).AddMinutes((-constants.MeterValueDatabaseSaveIntervalMinutes) * 2);
        return maxCacheDate;
    }

    private Dictionary<int, int>? GetCachedValues(MeterValueKind meterValueKind, bool predictedValue, DateOnly date)
    {
        //Do not log this as it is super noisy
        //logger.LogTrace("{method}({meterValueKind}, {predictedValue}, {date})", nameof(GetCachedValues), meterValueKind, predictedValue, date);
        var key = GetCacheKey(meterValueKind, predictedValue, date);
        if (memoryCache.TryGetValue(key, out Dictionary<int, int>? value))
        {
            return value;
        }
        return default;
    }

    private void CacheValues(MeterValueKind meterValueKind, bool predictedValue, DateOnly date, Dictionary<int, int> values)
    {
        logger.LogTrace("{method}({meterValueKind}, {predictedValue}, {date}, {values})",
            nameof(CacheValues), meterValueKind, predictedValue, date, values);
        var key = GetCacheKey(meterValueKind, predictedValue, date);
        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
        var currentlocalDate = DateOnly.FromDateTime(currentDate.LocalDateTime);
        SetCacheValue(currentlocalDate >= date, values, key);
    }

    private int? GetCachedValue(MeterValueKind meterValueKind, bool predictedValue, DateOnly date, int hour)
    {
        var key = GetCacheKey(meterValueKind, predictedValue, date, hour);
        if (memoryCache.TryGetValue(key, out int? value))
        {
            logger.LogTrace("Cached value found for key {key}", key);
            return value;
        }
        logger.LogTrace("No cached value found for key {key}", key);
        return default;
    }

    private void CacheValue(MeterValueKind meterValueKind, bool predictedValue, DateOnly date, int hour, int value)
    {
        logger.LogTrace("{method}({meterValueKind}, {predictedValue}, {date}, {hour}, {value})",
            nameof(CacheValue), meterValueKind, predictedValue, date, hour, value);
        var key = GetCacheKey(meterValueKind, predictedValue, date, hour);
        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
        var currentlocalDate = DateOnly.FromDateTime(currentDate.LocalDateTime);
        SetCacheValue(currentlocalDate >= date, value, key);
    }

    private void SetCacheValue(bool shouldExpire, object value, string key)
    {
        var options = new MemoryCacheEntryOptions();
        if (shouldExpire)
        {
            options.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(constants.WeatherDateRefreshIntervallHours + 1);
        }
        else
        {
            options.SlidingExpiration = TimeSpan.FromDays(90);
        }
        memoryCache.Set(key, value, options);
    }

    private string GetCacheKey(MeterValueKind meterValueKind, bool predictedValue, DateOnly date, int? hour = null)
    {
        var key = $"{meterValueKind}_{predictedValue}_{date:yyyyMMdd}";
        if (hour != null)
        {
            key += $"_{hour}";
        }
        return key;
    }

    private MeterValue? GetCachedMeterValue(MeterValueKind meterValueKind, DateTimeOffset hourlyTimeStamp)
    {
        var key = GetMeterValueCacheKey(meterValueKind, hourlyTimeStamp);
        if (memoryCache.TryGetValue(key, out MeterValue? value))
        {
            logger.LogTrace("Cached value found for key {key}", key);
            return value;
        }
        logger.LogTrace("No cached value found for key {key}", key);
        return default;
    }

    private void CacheMeterValue(MeterValueKind meterValueKind, DateTimeOffset hourlyTimeStamp, MeterValue value)
    {
        logger.LogTrace("{method}({meterValueKind}, {hourlyTimeStamp}, {value})",
            nameof(CacheMeterValue), meterValueKind, hourlyTimeStamp, value);
        var key = GetMeterValueCacheKey(meterValueKind, hourlyTimeStamp);
        SetCacheValue(false, value, key);
    }

    private string GetMeterValueCacheKey(MeterValueKind meterValueKind, DateTimeOffset dateTimeOffset)
    {
        var key = $"{meterValueKind}_{dateTimeOffset}";
        return key;
    }

    private (DateTimeOffset utcPredictionStart, DateTimeOffset utcPredictionEnd, DateTimeOffset historicPredictionsSearchStart) ComputePredictionTimes(DateOnly date)
    {
        var localPredictionStart = date.ToDateTime(TimeOnly.MinValue);
        var localStartOffset = new DateTimeOffset(localPredictionStart, TimeZoneInfo.Local.GetUtcOffset(localPredictionStart));
        var utcPredictionStart = localStartOffset.ToUniversalTime();
        var utcPredictionEnd = utcPredictionStart.AddDays(1);
        const int predictionStartSearchDaysBeforePredictionStart = 21; // three weeks
        var historicPredictionsSearchStart = utcPredictionStart.AddDays(-predictionStartSearchDaysBeforePredictionStart);
        return (utcPredictionStart, utcPredictionEnd, historicPredictionsSearchStart);
    }

    private async Task<List<SolarRadiation>> GetLatestSolarRadiationsAsync(DateTimeOffset historicStart, DateTimeOffset utcPredictionEnd,
        CancellationToken cancellationToken)
    {
        var latestRadiations = await context.SolarRadiations
            .Where(r => r.Start >= historicStart && r.End <= utcPredictionEnd)
            .GroupBy(r => new { r.Start, r.End })
            .Select(g => g.OrderByDescending(r => r.CreatedAt).First())
            .AsNoTracking()
            .ToListAsync(cancellationToken: cancellationToken);

        return latestRadiations.OrderBy(r => r.Start).ToList();
    }

    private Dictionary<int, int> CreateHourlyDictionary(Dictionary<DateTimeOffset, int> inputs)
    {
        var result = new Dictionary<int, int>();
        foreach (var input in inputs)
        {
            result[input.Key.LocalDateTime.Hour] = input.Value;
        }

        return result;
    }

    private async Task<Dictionary<DateTimeOffset, int>> GetMeterEnergyDifferencesAsync(List<DateTimeOffset> hourlyTimeStamps,
        MeterValueKind meterValueKind, CancellationToken cancellationToken)
    {
        var createdWh = new Dictionary<DateTimeOffset, int>();

        //used for more efficient querying instead of list
        var timeStampLookup = new HashSet<DateTimeOffset>(hourlyTimeStamps);
        var missingHours = new List<DateTimeOffset>();

        foreach (var time in hourlyTimeStamps)
        {
            var nextHour = time.AddHours(1);
            if (!timeStampLookup.Contains(nextHour))
            {
                missingHours.Add(nextHour);
            }
        }
        var hoursToGetMeterValues = hourlyTimeStamps.Union(missingHours).ToList();
        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
        var maxDbConcurrency = 5;
        var throttler = new SemaphoreSlim(maxDbConcurrency);

        var queryTasks = hoursToGetMeterValues.Select(async dateTimeOffset =>
        {
            // wait our turn
            await throttler.WaitAsync(cancellationToken);
            try
            {
                using var scope = serviceProvider.CreateScope();
                var scopedCtx = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
                var minimumAge = dateTimeOffset.AddHours(-1);

                var meterValue = GetCachedMeterValue(meterValueKind, dateTimeOffset);
                if (meterValue == default && currentDate > dateTimeOffset)
                {
                    meterValue = await scopedCtx.MeterValues
                        .Where(m => m.MeterValueKind == meterValueKind
                                    && m.Timestamp <= dateTimeOffset
                                    && m.Timestamp > minimumAge)
                        .OrderByDescending(m => m.Id)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (meterValue != default && meterValue.Timestamp < GetMaxCacheDate())
                        CacheMeterValue(meterValueKind, dateTimeOffset, meterValue);

                    // optional: small delay so you don’t slam the DB in bursts
                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                }

                return new { Timestamp = dateTimeOffset, MeterValue = meterValue };
            }
            finally
            {
                throttler.Release();
            }
        });

        var results = await Task.WhenAll(queryTasks);
        var orderedResults = results.ToDictionary(result => result.Timestamp, result => result.MeterValue);

        foreach (var hourlyTimeStamp in hourlyTimeStamps)
        {
            var meterValue = orderedResults[hourlyTimeStamp];
            var nextMeterValue = orderedResults[hourlyTimeStamp.AddHours(1)];

            if (nextMeterValue != default && meterValue != default)
            {
                var energyDifference = Convert.ToInt32((nextMeterValue.EstimatedEnergyWs - meterValue.EstimatedEnergyWs) / 3600);
                createdWh.Add(hourlyTimeStamp, energyDifference);
            }
        }

        return createdWh;
    }

    private Dictionary<int, double> ComputeWeightedAverageFactors(
        List<DateTimeOffset> hourlyTimeStamps,
        Dictionary<DateTimeOffset, int> createdWh,
        List<SolarRadiation> latestRadiations,
        DateTimeOffset historicStart)
    {
        // Compute weighted conversion factors per UTC hour.
        var hourlyFactorsWeighted = new Dictionary<int, List<(double factor, double weight)>>();

        foreach (var hourStamp in hourlyTimeStamps)
        {
            if (!createdWh.TryGetValue(hourStamp, out var producedWh))
            {
                continue; // skip if no produced energy sample
            }

            // Find the matching solar radiation record for the same UTC year, day, and hour.
            var matchingRadiation = latestRadiations.FirstOrDefault(r =>
                r.Start.UtcDateTime.Year == hourStamp.UtcDateTime.Year &&
                r.Start.UtcDateTime.DayOfYear == hourStamp.UtcDateTime.DayOfYear &&
                r.Start.UtcDateTime.Hour == hourStamp.UtcDateTime.Hour);

            if (matchingRadiation == null || matchingRadiation.SolarRadiationWhPerM2 <= 0)
            {
                continue;
            }

            // Calculate conversion factor: produced Wh per unit of solar radiation.
            double factor = producedWh / matchingRadiation.SolarRadiationWhPerM2;

            // Compute a weight based on recency (older samples get lower weight).
            var weight = 1 + (hourStamp.UtcDateTime - historicStart.UtcDateTime).TotalDays;

            var hour = hourStamp.UtcDateTime.Hour;
            if (!hourlyFactorsWeighted.ContainsKey(hour))
            {
                hourlyFactorsWeighted[hour] = new List<(double factor, double weight)>();
            }

            hourlyFactorsWeighted[hour].Add((factor, weight));
        }

        // Compute the weighted average conversion factor for each UTC hour.
        var avgHourlyWeightedFactors = new Dictionary<int, double>();
        foreach (var kvp in hourlyFactorsWeighted)
        {
            var hour = kvp.Key;
            var weightedSamples = kvp.Value;
            var weightedSum = weightedSamples.Sum(item => item.factor * item.weight);
            var weightTotal = weightedSamples.Sum(item => item.weight);
            avgHourlyWeightedFactors[hour] = weightedSum / weightTotal;
        }

        return avgHourlyWeightedFactors;
    }

    private Dictionary<int, int> ComputeWeightedMeterValueChanges(
    List<DateTimeOffset> hourlyTimeStamps,
    Dictionary<DateTimeOffset, int> createdWh,
    DateTimeOffset historicStart)
    {
        // Compute weighted conversion factors per UTC hour.
        var hourlyFactorsWeighted = new Dictionary<int, List<(double meterValueChange, double weight)>>();

        foreach (var hourStamp in hourlyTimeStamps)
        {
            if (!createdWh.TryGetValue(hourStamp, out var producedWh))
            {
                continue; // skip if no produced energy sample
            }

            // Compute a weight based on recency (older samples get lower weight).
            var weight = 1 + (hourStamp.UtcDateTime - historicStart.UtcDateTime).TotalDays;

            var hour = hourStamp.LocalDateTime.Hour;
            if (!hourlyFactorsWeighted.ContainsKey(hour))
            {
                hourlyFactorsWeighted[hour] = new List<(double meterValueChange, double weight)>();
            }

            hourlyFactorsWeighted[hour].Add((producedWh, weight));
        }

        // Compute the weighted average conversion factor for each UTC hour.
        var avgHourlyWeightedFactors = new Dictionary<int, int>();
        foreach (var kvp in hourlyFactorsWeighted)
        {
            var hour = kvp.Key;
            var weightedSamples = kvp.Value;
            var weightedSum = weightedSamples.Sum(item => item.meterValueChange * item.weight);
            var weightTotal = weightedSamples.Sum(item => item.weight);
            avgHourlyWeightedFactors[hour] = (int)(weightedSum / weightTotal);
        }

        return avgHourlyWeightedFactors;
    }

    private async Task<List<SolarRadiation>> GetForecastSolarRadiationsAsync(DateTimeOffset utcPredictionStart,
        DateTimeOffset utcPredictionEnd, CancellationToken cancellationToken)
    {
        var forecastSolarRadiations = await context.SolarRadiations
            .Where(r => r.Start >= utcPredictionStart && r.End <= utcPredictionEnd)
            .GroupBy(r => new { r.Start, r.End })
            .Select(g => g.OrderByDescending(r => r.CreatedAt).First())
            .AsNoTracking()
            .ToListAsync(cancellationToken: cancellationToken);

        return forecastSolarRadiations;
    }

    private Dictionary<int, int> ComputePredictedProduction(
        List<SolarRadiation> forecastSolarRadiations,
        Dictionary<int, double> avgHourlyWeightedFactors)
    {
        var predictedProduction = new Dictionary<int, int>();

        foreach (var forecast in forecastSolarRadiations)
        {
            var forecastHour = forecast.Start.UtcDateTime.Hour;
            if (!avgHourlyWeightedFactors.TryGetValue(forecastHour, out var factor))
            {
                continue; // skip hours without historical samples
            }

            // Calculate predicted energy produced in Wh and then convert to kWh.
            var predictedWh = forecast.SolarRadiationWhPerM2 * factor;
            predictedProduction.Add(forecast.Start.LocalDateTime.Hour, (int)predictedWh);
        }

        return predictedProduction;
    }

    private List<DateTimeOffset> GetHourlyTimestamps(DateTimeOffset start, DateTimeOffset end)
    {
        if (start > end)
        {
            throw new ArgumentException("The start value must be earlier than the end value.");
        }

        if (start.Offset != TimeSpan.Zero || end.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Both DateTimeOffset values need to be UTC timed");
        }

        var firstHour = new DateTimeOffset(start.Year, start.Month, start.Day, start.Hour, 0, 0, start.Offset);
        var hourlyList = new List<DateTimeOffset>();

        for (var current = firstHour; current < end; current = current.AddHours(1))
        {
            hourlyList.Add(current);
        }

        return hourlyList;
    }
}
