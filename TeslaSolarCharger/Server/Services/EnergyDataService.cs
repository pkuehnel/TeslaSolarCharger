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

    private const int HistoricPredictionsSearchDaysBeforePredictionStart = 21; // three weeks

    public async Task RefreshCachedValues(CancellationToken contextCancellationToken)
    {
        logger.LogTrace("{method}()", nameof(RefreshCachedValues));
        var cacheInPastDays = 10;
        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow().Date;
        var currentUtcDay = new DateTimeOffset(currentDate, TimeSpan.Zero);
        var stopWatch = new Stopwatch();
        stopWatch.Start();
        for (var i = -cacheInPastDays; i <= constants.WeatherPredictionInFutureDays; i++)
        {
            var startDate = currentUtcDay.AddDays(i);
            await GetPredictedSolarProductionByLocalHour(startDate, startDate.AddDays(1), TimeSpan.FromHours(1), contextCancellationToken).ConfigureAwait(false);
            await GetPredictedHouseConsumptionByLocalHour(startDate, startDate.AddDays(1), TimeSpan.FromHours(1), contextCancellationToken).ConfigureAwait(false);
            await GetActualSolarProductionByLocalHour(startDate, startDate.AddDays(1), TimeSpan.FromHours(1), contextCancellationToken).ConfigureAwait(false);
            await GetActualHouseConsumptionByLocalHour(startDate, startDate.AddDays(1), TimeSpan.FromHours(1), contextCancellationToken).ConfigureAwait(false);
        }
        stopWatch.Stop();
        logger.LogInformation("Cache refresh took {elapsed}", stopWatch.Elapsed);
    }

    public async Task<Dictionary<DateTimeOffset, int>> GetPredictedSolarProductionByLocalHour(DateTimeOffset startDate, DateTimeOffset endDate, TimeSpan sliceLength, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({startDate}, {endDate}, {sliceLength})", nameof(GetPredictedSolarProductionByLocalHour), startDate, endDate, sliceLength);
        if (configurationWrapper.UseFakeEnergyPredictions())
        {
            var fakedResult = GenerateFakeResult(startDate, endDate, sliceLength);
            return fakedResult;
        }
        //ToDo: do not get historic values for all days but only the timespans between startDate and endDate (e.g. if start is10:00 and end is 12:00, only get historic values between 10 and 12 on each day)
        var historicValueTimeStamps = GenerateSlicedTimeStamps(startDate.AddDays(-HistoricPredictionsSearchDaysBeforePredictionStart), startDate, sliceLength);
        var energyMeterDifferences = await GetMeterEnergyDifferencesAsync(historicValueTimeStamps, sliceLength, MeterValueKind.SolarGeneration, cancellationToken);

        var historicPredictionsSearchStart = startDate.AddDays(-HistoricPredictionsSearchDaysBeforePredictionStart);
        var latestRadiations = await GetSlicedSolarRadiationValues(historicPredictionsSearchStart, startDate, sliceLength, cancellationToken);
        var avgHourlyWeightedFactors = ComputeWeightedAverageFactors(historicValueTimeStamps, energyMeterDifferences, latestRadiations);
        var forecastSolarRadiations = await GetSlicedSolarRadiationValues(startDate, endDate, sliceLength, cancellationToken);
        var resultTimeStamps = GenerateSlicedTimeStamps(startDate, endDate, sliceLength);

        var predictedProduction = ComputePredictedProduction(forecastSolarRadiations, avgHourlyWeightedFactors, resultTimeStamps);

        var result = predictedProduction.OrderBy(x => x.Key).ToDictionary(x => x.Key, y => y.Value);
        return result;
    }

    public async Task<Dictionary<DateTimeOffset, int>> GetPredictedHouseConsumptionByLocalHour(DateTimeOffset startDate, DateTimeOffset endDate, TimeSpan sliceLength, CancellationToken httpContextRequestAborted)
    {
        logger.LogTrace("{method}({startDate}, {endDate}, {sliceLength})", nameof(GetPredictedHouseConsumptionByLocalHour), startDate, endDate, sliceLength);

        if (configurationWrapper.UseFakeEnergyPredictions())
        {
            var fakedResult = GenerateFakeResult(startDate, endDate, sliceLength);
            return fakedResult;
        }
        //ToDo: do not get historic values for all days but only the timespans between startDate and endDate (e.g. if start is10:00 and end is 12:00, only get historic values between 10 and 12 on each day)
        var historicValueTimeStamps = GenerateSlicedTimeStamps(startDate.AddDays(-HistoricPredictionsSearchDaysBeforePredictionStart), startDate, sliceLength);
        var energyMeterDifferences = await GetMeterEnergyDifferencesAsync(historicValueTimeStamps, sliceLength, MeterValueKind.HouseConsumption, httpContextRequestAborted);
        var averageChangeAtTimeSpan = ComputeWeightedMeterValueChanges(historicValueTimeStamps, energyMeterDifferences);

        var resultTimeStamps = GenerateSlicedTimeStamps(startDate, endDate, sliceLength);
        var predictedHouseConsumption = new Dictionary<DateTimeOffset, int>();
        foreach (var timeStamp in resultTimeStamps)
        {
            if (averageChangeAtTimeSpan.TryGetValue(timeStamp.TimeOfDay, out var averageChange))
            {
                predictedHouseConsumption[timeStamp] = averageChange;
            }
            else
            {
                predictedHouseConsumption[timeStamp] = 0; // Default to 0 if no average change is found
            }
        }
        return predictedHouseConsumption;
    }

    public async Task<Dictionary<DateTimeOffset, int>> GetActualSolarProductionByLocalHour(DateTimeOffset startDate, DateTimeOffset endDate, TimeSpan sliceLength, CancellationToken httpContextRequestAborted)
    {
        logger.LogTrace("{method}({startDate}, {endDate}, {sliceLength})", nameof(GetActualSolarProductionByLocalHour), startDate, endDate, sliceLength);
        if (configurationWrapper.UseFakeEnergyHistory())
        {
            return GenerateFakeResult(startDate, endDate, sliceLength);
        }
        return await GetActualValues(MeterValueKind.SolarGeneration, startDate, endDate, sliceLength, httpContextRequestAborted);
    }

    public async Task<Dictionary<DateTimeOffset, int>> GetActualHouseConsumptionByLocalHour(DateTimeOffset startDate, DateTimeOffset endDate, TimeSpan sliceLength, CancellationToken httpContextRequestAborted)
    {
        logger.LogTrace("{method}({startDate}, {endDate}, {sliceLength})", nameof(GetActualHouseConsumptionByLocalHour), startDate, endDate, sliceLength);
        if (configurationWrapper.UseFakeEnergyHistory())
        {
            return GenerateFakeResult(startDate, endDate, sliceLength);
        }
        return await GetActualValues(MeterValueKind.HouseConsumption, startDate, endDate, sliceLength, httpContextRequestAborted);
    }

    public async Task<Dictionary<DateTimeOffset, int>> GetPredictedSurplusPerSlice(DateTimeOffset startDate, DateTimeOffset endDate, TimeSpan sliceLength, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({startDate}, {endDate}, {sliceLength})", nameof(GetPredictedSurplusPerSlice), startDate, endDate, sliceLength);
        if (configurationWrapper.UseFakeEnergyHistory())
        {
            return GenerateFakeResult(startDate, endDate, sliceLength);
        }
        var solarProductionTask = GetPredictedSolarProductionByLocalHour(startDate, endDate, sliceLength, cancellationToken);
        var houseConsumptionTask = GetPredictedHouseConsumptionByLocalHour(startDate, endDate, sliceLength, cancellationToken);
        var solarProduction = await solarProductionTask.ConfigureAwait(false);
        var houseConsumption = await houseConsumptionTask.ConfigureAwait(false);
        var surplusPerSlice = new Dictionary<DateTimeOffset, int>();
        foreach (var timeStamp in solarProduction.Keys)
        {
            var production = solarProduction.GetValueOrDefault(timeStamp, 0);
            var consumption = houseConsumption.GetValueOrDefault(timeStamp, 0);
            surplusPerSlice[timeStamp] = production - consumption;
        }
        return surplusPerSlice;
    }

    private Dictionary<DateTimeOffset, int> GenerateFakeResult(DateTimeOffset startDate, DateTimeOffset endDate, TimeSpan sliceLength)
    {
        var fakedResult = new Dictionary<DateTimeOffset, int>();
        var slicedTimeStamps = GenerateSlicedTimeStamps(startDate, endDate, sliceLength);
        foreach (var slicedTimeStamp in slicedTimeStamps)
        {
            fakedResult[slicedTimeStamp] = 1000; // Fake value of 1000 Wh for each slice
        }
        return fakedResult;
    }

    private List<DateTimeOffset> GenerateSlicedTimeStamps(
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        TimeSpan sliceLength)
    {
        if (sliceLength <= TimeSpan.Zero)
            throw new ArgumentException("Slice length must be greater than zero.", nameof(sliceLength));

        // 1 day
        var oneDay = TimeSpan.FromHours(24);

        // sliceLength cannot exceed 24h
        if (sliceLength > oneDay)
            throw new ArgumentException("Slice length cannot exceed 24 hours.", nameof(sliceLength));

        // 24h must be an exact multiple of sliceLength
        if (oneDay.Ticks % sliceLength.Ticks != 0)
            throw new ArgumentException(
                "24 hours must be an exact multiple of sliceLength.", nameof(sliceLength));

        if (startDate > endDate)
            throw new ArgumentOutOfRangeException(
                nameof(startDate), "Start date must be on or before end date.");

        var totalSpan = endDate - startDate;
        
        // Ensure the total span is an exact multiple of sliceLength
        if (totalSpan.Ticks % sliceLength.Ticks != 0)
            throw new ArgumentException(
                "The time span between startDate and endDate must be an exact multiple of sliceLength.");

        var slicedTimeStamps = new List<DateTimeOffset>();
        for (var current = startDate; current < endDate; current = current.Add(sliceLength))
        {
            slicedTimeStamps.Add(current);
        }
        return slicedTimeStamps;
    }

    private async Task<Dictionary<DateTimeOffset, int>> GetActualValues(MeterValueKind meterValueKind, DateTimeOffset startDate, DateTimeOffset endDate, TimeSpan sliceLength, CancellationToken cancellationToken)
    {
        var resultTimeStamps = GenerateSlicedTimeStamps(startDate, endDate, sliceLength);
        var dateTimeOffsetDictionaryFromDatabase = await GetMeterEnergyDifferencesAsync(resultTimeStamps, sliceLength, meterValueKind, cancellationToken);
        return dateTimeOffsetDictionaryFromDatabase;
    }

    private DateTimeOffset GetMaxCacheDate(TimeSpan sliceLength)
    {
        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
        //reduce by sliceLength as dateTimeOffsetDictionary key is sliceLength older than last relevant value within that slice
        //reduce by twice the save intervals to make sure values are only cached after they have been saved to the database
        var maxCacheDate = currentDate.AddTicks(-sliceLength.Ticks).AddMinutes((-constants.MeterValueDatabaseSaveIntervalMinutes) * 2);
        return maxCacheDate;
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

    private MeterValue? GetCachedMeterValue(MeterValueKind meterValueKind, DateTimeOffset hourlyTimeStamp, TimeSpan sliceLength)
    {
        var key = GetMeterValueCacheKey(meterValueKind, hourlyTimeStamp, sliceLength);
        if (memoryCache.TryGetValue(key, out MeterValue? value))
        {
            return value;
        }
        return default;
    }

    private void CacheMeterValue(MeterValueKind meterValueKind, DateTimeOffset hourlyTimeStamp, TimeSpan sliceLength, MeterValue value)
    {
        logger.LogTrace("{method}({meterValueKind}, {hourlyTimeStamp}, {value})",
            nameof(CacheMeterValue), meterValueKind, hourlyTimeStamp, value);
        var key = GetMeterValueCacheKey(meterValueKind, hourlyTimeStamp, sliceLength);
        SetCacheValue(false, value, key);
    }

    private string GetMeterValueCacheKey(MeterValueKind meterValueKind, DateTimeOffset dateTimeOffset, TimeSpan sliceLength)
    {
        var key = $"{meterValueKind}_{dateTimeOffset}_{sliceLength}";
        return key;
    }

    private async Task<Dictionary<DateTimeOffset, float>> GetSlicedSolarRadiationValues(DateTimeOffset historicStart, DateTimeOffset utcPredictionEnd,
        TimeSpan sliceLength, CancellationToken cancellationToken)
    {
        var latestRadiations = await context.SolarRadiations
            .Where(r => r.Start >= historicStart && r.End <= utcPredictionEnd)
            .GroupBy(r => new { r.Start, r.End })
            .Select(g => g.OrderByDescending(r => r.CreatedAt).First())
            .AsNoTracking()
            .ToListAsync(cancellationToken: cancellationToken);
        var result = new Dictionary<DateTimeOffset, float>();
        for (var currentStartDate = historicStart; currentStartDate < utcPredictionEnd; currentStartDate += sliceLength)
        {
            var currentEndDate = currentStartDate + sliceLength;
            var matchingRadiations = latestRadiations
                .Where(r => r.End >= currentStartDate && r.Start < currentEndDate)
                .ToList();
            if (matchingRadiations.Count == 0)
            {
                continue;
            }
            var radiationValue = 0f;
            foreach (var matchingRadiation in matchingRadiations)
            {
                var overlapDuration = GetOverlapDuration(currentStartDate, currentEndDate, matchingRadiation.Start, matchingRadiation.End);
                var normalizedRadiationValue = matchingRadiation.SolarRadiationWhPerM2 * (float)(overlapDuration.TotalSeconds / (matchingRadiation.End - matchingRadiation.Start).TotalSeconds);
                radiationValue += normalizedRadiationValue;
            }
            result[currentStartDate] = radiationValue;
        }

        return result;
    }

    private TimeSpan GetOverlapDuration(DateTimeOffset start1, DateTimeOffset end1, DateTimeOffset start2, DateTimeOffset end2)
    {
        var overlapStart = start1 > start2 ? start1 : start2;
        var overlapEnd = end1 < end2 ? end1 : end2;
        if (overlapStart < overlapEnd)
        {
            return overlapEnd - overlapStart;
        }
        return TimeSpan.Zero; // No overlap
    }

    private async Task<Dictionary<DateTimeOffset, int>> GetMeterEnergyDifferencesAsync(List<DateTimeOffset> slicedTimeStamps,
        TimeSpan sliceLength,
        MeterValueKind meterValueKind, CancellationToken cancellationToken)
    {
        var createdWh = new Dictionary<DateTimeOffset, int>();
        var currentDate = dateTimeProvider.DateTimeOffSetUtcNow();
        var maxDbConcurrency = 5;
        var throttler = new SemaphoreSlim(maxDbConcurrency);

        var timeStampsToGetMeterValuesFrom = slicedTimeStamps.ToList();
        var lastSlicedTimeStamp = slicedTimeStamps.Last();
        timeStampsToGetMeterValuesFrom.Add(lastSlicedTimeStamp + sliceLength);

        var queryTasks = timeStampsToGetMeterValuesFrom.Select(async dateTimeOffset =>
        {
            // wait our turn
            await throttler.WaitAsync(cancellationToken);
            try
            {
                using var scope = serviceProvider.CreateScope();
                var scopedContext = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
                var minimumAge = dateTimeOffset - sliceLength;

                var meterValue = GetCachedMeterValue(meterValueKind, dateTimeOffset, sliceLength);
                if (meterValue == default && currentDate > dateTimeOffset)
                {
                    meterValue = await scopedContext.MeterValues
                        .Where(m => m.MeterValueKind == meterValueKind
                                    && m.Timestamp <= dateTimeOffset
                                    && m.Timestamp > minimumAge)
                        .OrderByDescending(m => m.Id)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(cancellationToken);

                    if (meterValue != default && meterValue.Timestamp < GetMaxCacheDate(sliceLength))
                        CacheMeterValue(meterValueKind, dateTimeOffset, sliceLength, meterValue);

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

        foreach (var slicedTimeStamp in slicedTimeStamps)
        {
            var meterValue = orderedResults[slicedTimeStamp];
            var nextMeterValue = orderedResults[slicedTimeStamp + sliceLength];

            if (nextMeterValue != default && meterValue != default)
            {
                var energyDifference = Convert.ToInt32((nextMeterValue.EstimatedEnergyWs - meterValue.EstimatedEnergyWs) / 3600);
                createdWh.Add(slicedTimeStamp, energyDifference);
            }
        }

        return createdWh;
    }

    private Dictionary<TimeSpan, double> ComputeWeightedAverageFactors(
        List<DateTimeOffset> historicTimeStamps,
        Dictionary<DateTimeOffset, int> energyMeterDifferences,
        Dictionary<DateTimeOffset, float> latestRadiations)
    {
        // Compute weighted conversion factors per UTC hour.
        var timebasedFactorsWeighted = new Dictionary<TimeSpan, List<(double meterValueChangePerRadiation, double weight)>>();
        if (historicTimeStamps.Count < 1)
        {
            return new();
        }
        var historicStart = historicTimeStamps.First();
        foreach (var hourStamp in historicTimeStamps)
        {
            if (!energyMeterDifferences.TryGetValue(hourStamp, out var energyDifferenceWh))
            {
                continue; // skip if no produced energy sample
            }
            if(!latestRadiations.TryGetValue(hourStamp, out var radiationValue))
            {
                continue; // skip if no radiation sample
            }

            // Compute a weight based on recency (older samples get lower weight).
            var weight = 1 + (hourStamp.UtcDateTime - historicStart.UtcDateTime).TotalDays;

            var timeOfDay = hourStamp.TimeOfDay;
            if (!timebasedFactorsWeighted.ContainsKey(timeOfDay))
            {
                timebasedFactorsWeighted[timeOfDay] = new List<(double meterValueChangePerRadiation, double weight)>();
            }

            if (radiationValue > 0)
            {
                timebasedFactorsWeighted[timeOfDay].Add((energyDifferenceWh / radiationValue, weight));
            }
        }

        // Compute the weighted average conversion factor for each UTC hour.
        var avgHourlyWeightedFactors = new Dictionary<TimeSpan, double>();
        foreach (var kvp in timebasedFactorsWeighted)
        {
            var timeSpan = kvp.Key;
            var weightedSamples = kvp.Value;
            var weightedSum = weightedSamples.Sum(item => item.meterValueChangePerRadiation * item.weight);
            var weightTotal = weightedSamples.Sum(item => item.weight);
            avgHourlyWeightedFactors[timeSpan] = (weightedSum / weightTotal);
        }
        return avgHourlyWeightedFactors;
    }

    private Dictionary<TimeSpan, int> ComputeWeightedMeterValueChanges(
    List<DateTimeOffset> historicTimeStamps,
    Dictionary<DateTimeOffset, int> historicEnergyMeterDifferences)
    {
        // Compute weighted conversion factors per UTC hour.
        var hourlyFactorsWeighted = new Dictionary<TimeSpan, List<(double meterValueChange, double weight)>>();
        if (historicTimeStamps.Count < 1)
        {
            return new();
        }
        var historicStart = historicTimeStamps.First();
        foreach (var hourStamp in historicTimeStamps)
        {
            if (!historicEnergyMeterDifferences.TryGetValue(hourStamp, out var energyDifferenceWh))
            {
                continue; // skip if no produced energy sample
            }

            // Compute a weight based on recency (older samples get lower weight).
            var weight = 1 + (hourStamp.UtcDateTime - historicStart.UtcDateTime).TotalDays;

            var timeOfDay = hourStamp.TimeOfDay;
            if (!hourlyFactorsWeighted.ContainsKey(timeOfDay))
            {
                hourlyFactorsWeighted[timeOfDay] = new List<(double meterValueChange, double weight)>();
            }

            hourlyFactorsWeighted[timeOfDay].Add((energyDifferenceWh, weight));
        }

        // Compute the weighted average conversion factor for each UTC hour.
        var avgHourlyWeightedFactors = new Dictionary<TimeSpan, int>();
        foreach (var kvp in hourlyFactorsWeighted)
        {
            var timeSpan = kvp.Key;
            var weightedSamples = kvp.Value;
            var weightedSum = weightedSamples.Sum(item => item.meterValueChange * item.weight);
            var weightTotal = weightedSamples.Sum(item => item.weight);
            avgHourlyWeightedFactors[timeSpan] = (int)(weightedSum / weightTotal);
        }

        return avgHourlyWeightedFactors;
    }

    private Dictionary<DateTimeOffset, int> ComputePredictedProduction(Dictionary<DateTimeOffset, float> forecastSolarRadiations,
        Dictionary<TimeSpan, double> avgHourlyWeightedFactors, List<DateTimeOffset> resultTimeStamps)
    {
        var predictedProduction = new Dictionary<DateTimeOffset, int>();
        foreach (var resultTimeStamp in resultTimeStamps)
        {
            var factor = avgHourlyWeightedFactors.GetValueOrDefault(resultTimeStamp.TimeOfDay, 0.0);
            if (factor == 0.0)
            {
                predictedProduction[resultTimeStamp] = 0; // No historical data for this hour
                continue;
            }
            if (!forecastSolarRadiations.TryGetValue(resultTimeStamp, out var radiationValue))
            {
                predictedProduction[resultTimeStamp] = 0; // No forecast data for this hour
                continue;
            }
            var predictedWh = radiationValue * factor;
            predictedProduction[resultTimeStamp] = (int)predictedWh;
        }
        return predictedProduction;
    }
}
