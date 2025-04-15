using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using System.Reactive;
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
    IServiceProvider serviceProvider) : IEnergyDataService
{
    private readonly string _solarPredictionCachePrefix = "SolarPrediction_";
    private readonly string _housePredictionCachePrefix = "HousePrediction_";
    private readonly string _solarActualCachePrefix = "SolarActual_";
    private readonly string _houseActualCachePrefix = "HouseActual_";

    public async Task<Dictionary<int, int>> GetPredictedSolarProductionByLocalHour(DateOnly date, CancellationToken cancellationToken, bool useCache)
    {
        logger.LogTrace("{method}({date}, {useCache})", nameof(GetPredictedSolarProductionByLocalHour), date, useCache);
        if (useCache)
        {
            var cachedResult = GetSolarPredictionFromCache(date);
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
        CacheSolarPrediction(date, predictedProduction);
        return predictedProduction.OrderBy(x => x.Key).ToDictionary(x => x.Key, y => y.Value);
    }

    public async Task<Dictionary<int, int>> GetPredictedHouseConsumptionByLocalHour(DateOnly date,
        CancellationToken httpContextRequestAborted, bool useCache)
    {
        logger.LogTrace("{method}({date}, {useCache})", nameof(GetPredictedHouseConsumptionByLocalHour), date, useCache);
        if (useCache)
        {
            var cachedResult = GetHousePredictionFromCache(date);
            if (cachedResult != default)
            {
                return cachedResult;
            }
        }

        var (utcPredictionStart, _, historicPredictionsSearchStart) = ComputePredictionTimes(date);
        var hourlyTimeStamps = GetHourlyTimestamps(historicPredictionsSearchStart, utcPredictionStart);
        var createdWh = await GetMeterEnergyDifferencesAsync(hourlyTimeStamps, MeterValueKind.HouseConsumption, httpContextRequestAborted);
        var result = ComputeWeightedMeterValueChanges(hourlyTimeStamps, createdWh, historicPredictionsSearchStart);
        CacheHousePrediction(date, result);
        return result.OrderBy(x => x.Key).ToDictionary(x => x.Key, y => y.Value);
    }

    public async Task<Dictionary<int, int>> GetActualSolarProductionByLocalHour(DateOnly date, CancellationToken httpContextRequestAborted, bool useCache)
    {
        logger.LogTrace("{method}({date}, {useCache})", nameof(GetActualSolarProductionByLocalHour), date, useCache);
        var (utcPredictionStart, utcPredictionEnd, _) = ComputePredictionTimes(date);
        var hourlyTimeStamps = GetHourlyTimestamps(utcPredictionStart, utcPredictionEnd);
        var createdWh = await GetMeterEnergyDifferencesAsync(hourlyTimeStamps, MeterValueKind.SolarGeneration, httpContextRequestAborted);
        var result = CreateHourlyDictionary(createdWh);
        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
        //Only cache values if they are already saved in the database for sure
        var savedMeterValuesHour = currentDate.LocalDateTime.AddMinutes((-constants.MeterValueDatabaseSaveIntervallMinutes)*2).Hour;
        foreach (var i in result)
        {
            if(i.Key > savedMeterValuesHour)
            {
                continue;
            }
            CacheHistoricSolarValue(date, i.Key, i.Value);
        }
        return result.OrderBy(x => x.Key).ToDictionary(x => x.Key, y => y.Value);
    }

    public async Task<Dictionary<int, int>> GetActualHouseConsumptionByLocalHour(DateOnly date, CancellationToken httpContextRequestAborted, bool useCache)
    {
        logger.LogTrace("{method}({date})", nameof(GetActualHouseConsumptionByLocalHour), date);
        var (utcPredictionStart, utcPredictionEnd, _) = ComputePredictionTimes(date);
        var resultHours = GetHourlyTimestamps(utcPredictionStart, utcPredictionEnd);
        var hoursToGetEnergyMeterDifferencesFrom = resultHours.ToList();
        var dateTimeOffsetDictionary = new Dictionary<DateTimeOffset, int>();
        if (useCache)
        {
            foreach (var hourlyTimeStamp in resultHours)
            {
                var hour = hourlyTimeStamp.ToLocalTime().Hour;
                var value = GetHouseHistoricValueFromCache(date, hour);
                if (value != default)
                {
                    dateTimeOffsetDictionary[hourlyTimeStamp] = value.Value;
                    hoursToGetEnergyMeterDifferencesFrom.Remove(hourlyTimeStamp);
                }
            }
        }
        var dateTimeOffsetDictionaryFromDatabase = await GetMeterEnergyDifferencesAsync(hoursToGetEnergyMeterDifferencesFrom, MeterValueKind.HouseConsumption, httpContextRequestAborted);
        foreach (var databaseValue in dateTimeOffsetDictionaryFromDatabase)
        {
            dateTimeOffsetDictionary[databaseValue.Key] = databaseValue.Value;
        }
        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
        var maxCacheDate = currentDate.AddMinutes((-constants.MeterValueDatabaseSaveIntervallMinutes) * 2);
        foreach (var dateTimeOffsetValue in dateTimeOffsetDictionary)
        {
            if (maxCacheDate >= dateTimeOffsetValue.Key)
            {
                CacheHistoricHouseValue(dateTimeOffsetValue.Key, dateTimeOffsetValue.Value);
            }
        }
        var result = CreateHourlyDictionary(dateTimeOffsetDictionary);
        return result.OrderBy(x => x.Key).ToDictionary(x => x.Key, y => y.Value);
    }

    private Dictionary<int, int>? GetSolarPredictionFromCache(DateOnly date)
    {
        logger.LogTrace("{method}({date})", nameof(GetSolarPredictionFromCache), date);
        var key = GetCacheKey(_solarPredictionCachePrefix, date);
        return GetCachedPrediction(key);
    }

    private Dictionary<int, int>? GetHousePredictionFromCache(DateOnly date)
    {
        logger.LogTrace("{method}({date})", nameof(GetHousePredictionFromCache), date);
        var key = GetCacheKey(_housePredictionCachePrefix, date);
        return GetCachedPrediction(key);
    }

    private Dictionary<int, int>? GetCachedPrediction(string key)
    {
        if (memoryCache.TryGetValue(key, out Dictionary<int, int>? values))
        {
            logger.LogTrace("Cached values found for key {key}", key);
            return values;
        }
        logger.LogTrace("No cached values found for key {key}", key);
        return default;
    }

    private void CacheSolarPrediction(DateOnly date, Dictionary<int, int> values)
    {
        logger.LogTrace("{method}({date}, {values})", nameof(CacheSolarPrediction), date, values);
        CachePrediction(_solarPredictionCachePrefix, date, values);
    }

    private void CacheHousePrediction(DateOnly date, Dictionary<int, int> values)
    {
        logger.LogTrace("{method}({date}, {values})", nameof(CacheHousePrediction), date, values);
        CachePrediction(_housePredictionCachePrefix, date, values);
    }

    private void CachePrediction(string prefix, DateOnly date, Dictionary<int, int> values)
    {
        logger.LogTrace("{method}({prefix}, {date}, {values})", nameof(CachePrediction), prefix, date, values);
        var key = GetCacheKey(prefix, date);
        var options = new MemoryCacheEntryOptions()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(constants.WeatherDateRefreshIntervallHours + 1),
        };
        memoryCache.Set(key, values, options);
    }

    private int? GetSolarHistoricValueFromCache(DateOnly date, int hour)
    {
        logger.LogTrace("{method}({date}, {hour})", nameof(GetSolarPredictionFromCache), date, hour);
        var key = GetCacheKey(_solarActualCachePrefix, date, hour);
        return GetCachedHistoricValue(key);
    }

    private int? GetHouseHistoricValueFromCache(DateOnly date, int hour)
    {
        logger.LogTrace("{method}({date}, {hour})", nameof(GetHousePredictionFromCache), date, hour);
        var key = GetCacheKey(_houseActualCachePrefix, date, hour);
        return GetCachedHistoricValue(key);
    }

    private int? GetCachedHistoricValue(string key)
    {
        if (memoryCache.TryGetValue(key, out int? value))
        {
            logger.LogTrace("Cached value found for key {key}", key);
            return value;
        }
        logger.LogTrace("No cached value found for key {key}", key);
        return default;
    }

    private void CacheHistoricHouseValue(DateTimeOffset timestamp, int value)
    {
        logger.LogTrace("{method}({timestamp}, {value})", nameof(CacheHistoricHouseValue), timestamp, value);
        var localDateTime = timestamp.LocalDateTime;
        var date = DateOnly.FromDateTime(localDateTime);
        var hour = localDateTime.Hour;
        CacheHistoricValue(_houseActualCachePrefix, date, hour, value);
    }

    private void CacheHistoricSolarValue(DateOnly date, int hour, int value)
    {
        logger.LogTrace("{method}({date}, {hour}, {value})", nameof(CacheHistoricSolarValue), date, hour, value);
        CacheHistoricValue(_solarActualCachePrefix, date, hour, value);
    }

    private void CacheHistoricValue(string prefix, DateOnly date, int hour, int value)
    {
        logger.LogTrace("{method}({prefix}, {date}, {hour}, {value})", nameof(CacheHistoricValue), prefix, date, hour, value);
        var key = GetCacheKey(prefix, date, hour);
        var options = new MemoryCacheEntryOptions()
        {
            SlidingExpiration = TimeSpan.FromDays(90),
        };
        memoryCache.Set(key, value, options);
    }

    private string GetCacheKey(string prefix, DateOnly date)
    {
        var key = $"{prefix}{date:yyyyMMdd}";
        return key;
    }

    private string GetCacheKey(string prefix, DateOnly date, int hour)
    {
        var key = $"{prefix}{date:yyyyMMdd}_{hour}";
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

        var queryTasks = hoursToGetMeterValues.Select(async dateTimeOffset =>
        {
            using var scope = serviceProvider.CreateScope();
            var scopedContext = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
            var minimumAge = dateTimeOffset.AddHours(-1);
            var meterValue = await scopedContext.MeterValues
                .Where(m => m.MeterValueKind == meterValueKind && m.Timestamp <= dateTimeOffset && m.Timestamp > minimumAge)
                .OrderByDescending(m => m.Id)
                .FirstOrDefaultAsync(cancellationToken);
            return new { Timestamp = dateTimeOffset, MeterValue = meterValue };
        });

        // Await all tasks concurrently.
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
