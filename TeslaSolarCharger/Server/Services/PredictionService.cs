using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class PredictionService(ILogger<PredictionService> logger,
    ITeslaSolarChargerContext context) : ISolarProductionPredictionService
{
    public async Task<Dictionary<DateTimeOffset, double>> GetPredictedSolarProductionByLocalHour(DateOnly date)
    {
        logger.LogTrace("{method}({date})", nameof(GetPredictedSolarProductionByLocalHour), date);
        var localPredictionStart = date.ToDateTime(TimeOnly.MinValue);
        var localStartOffset = new DateTimeOffset(localPredictionStart, TimeZoneInfo.Local.GetUtcOffset(localPredictionStart));
        var utcPredictionStart = localStartOffset.ToUniversalTime();
        var utcPredictionEnd = utcPredictionStart.AddDays(1);
        var predictionStartSearchDaysBeforePredictionStart = 21; //three weeeks
        var historicPredictionsSearchStart = utcPredictionStart.AddDays(-predictionStartSearchDaysBeforePredictionStart);
        var latestRadiations = await context.SolarRadiations
            .Where(r => r.Start >= historicPredictionsSearchStart && r.End <= utcPredictionEnd)
            .GroupBy(r => new { r.Start, r.End })
            .Select(g => g.OrderByDescending(r => r.CreatedAt).First())
            .ToListAsync();
        latestRadiations = latestRadiations.OrderBy(r => r.Start).ToList();
        var hourlyTimeStamps = GetHourlyTimestamps(historicPredictionsSearchStart, utcPredictionStart);
        var index = 0;
        var createdWh = new Dictionary<long, int>();
        MeterValue lastMeterValue = null;
        foreach (var hourlyTimeStamp in hourlyTimeStamps)
        {
            var meterValue = context.MeterValues
                .Where(m => m.MeterValueKind == MeterValueKind.SolarGeneration && m.Timestamp <= hourlyTimeStamp)
                .OrderByDescending(m => m.Id)
                .AsNoTracking()
                .FirstOrDefault();
            if (meterValue == default)
            {
                continue;
            }
            if (lastMeterValue != default)
            {
                createdWh.Add(hourlyTimeStamp.ToUnixTimeMilliseconds(), Convert.ToInt32((meterValue.EstimatedEnergyWs - lastMeterValue.EstimatedEnergyWs) / 3600));
            }
            lastMeterValue = meterValue;
        }

        // Step 1: Compute weighted conversion factors per UTC hour using historical data
        // We'll store for each hour a list of tuples: (conversion factor, weight)
        var hourlyFactorsWeighted = new Dictionary<int, List<(double factor, double weight)>>();

        foreach (var hourStamp in hourlyTimeStamps)
        {
            // Get produced energy (in Wh) for this hour.
            if (!createdWh.TryGetValue(hourStamp.ToUnixTimeMilliseconds(), out int producedWh))
                continue; // skip if no produced energy sample

            // Find the corresponding solar radiation record for the same UTC year, day, and hour.
            var matchingRadiation = latestRadiations.FirstOrDefault(r =>
                r.Start.UtcDateTime.Year == hourStamp.UtcDateTime.Year &&
                r.Start.UtcDateTime.DayOfYear == hourStamp.UtcDateTime.DayOfYear &&
                r.Start.UtcDateTime.Hour == hourStamp.UtcDateTime.Hour);

            // Skip if there is no matching radiation data or if the radiation value is zero or negative.
            if (matchingRadiation == null || matchingRadiation.SolarRadiationWhPerM2 <= 0)
                continue;

            // Calculate the conversion factor: produced Wh per unit of solar radiation (Wh/m²)
            double factor = producedWh / matchingRadiation.SolarRadiationWhPerM2;

            // Compute a weight based on recency.
            // Here, samples at the historic start get weight=1, and more recent samples get higher weight.
            var sampleDate = hourStamp.UtcDateTime;
            double weight = 1 + (sampleDate - historicPredictionsSearchStart.UtcDateTime).TotalDays;

            // Group conversion factors by the UTC hour
            int hour = hourStamp.UtcDateTime.Hour;
            if (!hourlyFactorsWeighted.ContainsKey(hour))
                hourlyFactorsWeighted[hour] = new List<(double factor, double weight)>();

            hourlyFactorsWeighted[hour].Add((factor, weight));
        }

        // Now, compute the weighted average conversion factor for each UTC hour.
        var avgHourlyWeightedFactors = new Dictionary<int, double>();
        foreach (var kvp in hourlyFactorsWeighted)
        {
            int hour = kvp.Key;
            var weightedSamples = kvp.Value;
            // Weighted average: sum(weight * factor) / sum(weight)
            double weightedSum = weightedSamples.Sum(item => item.factor * item.weight);
            double weightTotal = weightedSamples.Sum(item => item.weight);
            avgHourlyWeightedFactors[hour] = weightedSum / weightTotal;
        }

        // Step 2: Predict hourly production for the target date using forecasted solar radiation data.
        // Assume forecastSolarRadiations is a collection of forecast records with a Start timestamp and SolarRadiationWhPerM2 value.
        var predictedProduction = new Dictionary<DateTimeOffset, double>();

        var forecastSolarRadiations = await context.SolarRadiations
            .Where(r => r.Start >= utcPredictionStart && r.End <= utcPredictionEnd)
            .GroupBy(r => new { r.Start, r.End })
            .Select(g => g.OrderByDescending(r => r.CreatedAt).First())
            .ToListAsync();

        foreach (var forecast in forecastSolarRadiations)
        {
            int forecastHour = forecast.Start.UtcDateTime.Hour;
            // Only predict if a weighted conversion factor exists for this hour
            if (!avgHourlyWeightedFactors.TryGetValue(forecastHour, out double factor))
            {
                // Skip hours where we don't have historical samples.
                continue;
            }
            // Calculate predicted energy produced in Wh: forecasted radiation * conversion factor.
            double predictedWh = forecast.SolarRadiationWhPerM2 * factor;
            // Convert Wh to kWh.
            double predictedKWh = predictedWh / 1000.0;
            predictedProduction.Add(forecast.Start, predictedKWh);
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

        // Create a DateTimeOffset for the start hour (ignoring minutes, seconds, etc.)
        var firstHour = new DateTimeOffset(start.Year, start.Month, start.Day, start.Hour, 0, 0, start.Offset);

        var hourlyList = new List<DateTimeOffset>();
        for (var current = firstHour; current <= end; current = current.AddHours(1))
        {
            hourlyList.Add(current);
        }

        return hourlyList;
    }
}
