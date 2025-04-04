﻿using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class EnergyDataService(ILogger<EnergyDataService> logger,
    ITeslaSolarChargerContext context) : IEnergyDataService
{
    public async Task<Dictionary<int, int>> GetPredictedSolarProductionByLocalHour(DateOnly date)
    {
        logger.LogTrace("{method}({date})", nameof(GetPredictedSolarProductionByLocalHour), date);

        var (utcPredictionStart, utcPredictionEnd, historicPredictionsSearchStart) = ComputePredictionTimes(date);
        var hourlyTimeStamps = GetHourlyTimestamps(historicPredictionsSearchStart, utcPredictionStart);
        var createdWh = await GetMeterEnergyDifferencesAsync(hourlyTimeStamps, MeterValueKind.SolarGeneration);
        var latestRadiations = await GetLatestSolarRadiationsAsync(historicPredictionsSearchStart, utcPredictionEnd);
        var avgHourlyWeightedFactors = ComputeWeightedAverageFactors(hourlyTimeStamps, createdWh, latestRadiations, historicPredictionsSearchStart);
        var forecastSolarRadiations = await GetForecastSolarRadiationsAsync(utcPredictionStart, utcPredictionEnd);
        var predictedProduction = ComputePredictedProduction(forecastSolarRadiations, avgHourlyWeightedFactors);

        return predictedProduction;
    }

    public async Task<Dictionary<int, int>> GetPredictedHouseConsumptionByLocalHour(DateOnly date)
    {
        logger.LogTrace("{method}({date})", nameof(GetPredictedHouseConsumptionByLocalHour), date);
        var (utcPredictionStart, utcPredictionEnd, historicPredictionsSearchStart) = ComputePredictionTimes(date);
        var hourlyTimeStamps = GetHourlyTimestamps(historicPredictionsSearchStart, utcPredictionStart);
        var createdWh = await GetMeterEnergyDifferencesAsync(hourlyTimeStamps, MeterValueKind.HouseConsumption);
        var result = ComputeWeightedMeterValueChanges(hourlyTimeStamps, createdWh, historicPredictionsSearchStart);
        return result;
    }

    public async Task<Dictionary<int, int>> GetActualSolarProductionByLocalHour(DateOnly date)
    {
        logger.LogTrace("{method}({date})", nameof(GetActualSolarProductionByLocalHour), date);
        var (utcPredictionStart, utcPredictionEnd, historicPredictionsSearchStart) = ComputePredictionTimes(date);
        var hourlyTimeStamps = GetHourlyTimestamps(utcPredictionStart, utcPredictionEnd);
        var createdWh = await GetMeterEnergyDifferencesAsync(hourlyTimeStamps, MeterValueKind.SolarGeneration);
        var result = CreateHourlyDictionary(createdWh);
        return result;
    }

    public async Task<Dictionary<int, int>> GetActualHouseConsumptionByLocalHour(DateOnly date)
    {
        logger.LogTrace("{method}({date})", nameof(GetActualHouseConsumptionByLocalHour), date);
        var (utcPredictionStart, utcPredictionEnd, historicPredictionsSearchStart) = ComputePredictionTimes(date);
        var hourlyTimeStamps = GetHourlyTimestamps(utcPredictionStart, utcPredictionEnd);
        var createdWh = await GetMeterEnergyDifferencesAsync(hourlyTimeStamps, MeterValueKind.HouseConsumption);
        var result = CreateHourlyDictionary(createdWh);
        return result;
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

    private async Task<List<SolarRadiation>> GetLatestSolarRadiationsAsync(DateTimeOffset historicStart, DateTimeOffset utcPredictionEnd)
    {
        var latestRadiations = await context.SolarRadiations
            .Where(r => r.Start >= historicStart && r.End <= utcPredictionEnd)
            .GroupBy(r => new { r.Start, r.End })
            .Select(g => g.OrderByDescending(r => r.CreatedAt).First())
            .AsNoTracking()
            .ToListAsync();

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

    private async Task<Dictionary<DateTimeOffset, int>> GetMeterEnergyDifferencesAsync(List<DateTimeOffset> hourlyTimeStamps, MeterValueKind meterValueKind)
    {
        var createdWh = new Dictionary<DateTimeOffset, int>();
        MeterValue? lastMeterValue = null;

        foreach (var hourlyTimeStamp in hourlyTimeStamps)
        {
            var meterValue = await context.MeterValues
                .Where(m => m.MeterValueKind == meterValueKind && m.Timestamp <= hourlyTimeStamp)
                .OrderByDescending(m => m.Id)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (meterValue == default)
            {
                continue;
            }

            if (lastMeterValue != default)
            {
                var energyDifference = Convert.ToInt32((meterValue.EstimatedEnergyWs - lastMeterValue.EstimatedEnergyWs) / 3600);
                createdWh.Add(hourlyTimeStamp, energyDifference);
            }

            lastMeterValue = meterValue;
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

    private async Task<List<SolarRadiation>> GetForecastSolarRadiationsAsync(DateTimeOffset utcPredictionStart, DateTimeOffset utcPredictionEnd)
    {
        var forecastSolarRadiations = await context.SolarRadiations
            .Where(r => r.Start >= utcPredictionStart && r.End <= utcPredictionEnd)
            .GroupBy(r => new { r.Start, r.End })
            .Select(g => g.OrderByDescending(r => r.CreatedAt).First())
            .AsNoTracking()
            .ToListAsync();

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

        for (var current = firstHour; current <= end; current = current.AddHours(1))
        {
            hourlyList.Add(current);
        }

        return hourlyList;
    }
}
