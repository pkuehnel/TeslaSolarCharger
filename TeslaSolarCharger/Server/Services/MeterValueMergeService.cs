using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class MeterValueMergeService(
    ILogger<MeterValueMergeService> logger,
    ITeslaSolarChargerContext context,
    IDateTimeProvider dateTimeProvider) : IMeterValueMergeService
{
    public async Task MergeOldMeterValuesAsync(int olderThanDays, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{method}({olderThanDays})", nameof(MergeOldMeterValuesAsync), olderThanDays);

        var cutoffDate = dateTimeProvider.DateTimeOffSetUtcNow().AddDays(-olderThanDays);
        logger.LogInformation("Starting meter value merge for data older than {cutoffDate} ({olderThanDays} days)", cutoffDate, olderThanDays);

        // Get meter value kinds that are not related to cars or charging stations
        var meterValueKindsToMerge = new[]
        {
            MeterValueKind.SolarGeneration,
            MeterValueKind.HouseConsumption,
            MeterValueKind.HomeBatteryCharging,
            MeterValueKind.HomeBatteryDischarging,
            MeterValueKind.PowerToGrid,
            MeterValueKind.PowerFromGrid,
        };

        foreach (var meterValueKind in meterValueKindsToMerge)
        {
            await MergeMeterValueKindAsync(meterValueKind, cutoffDate, cancellationToken);
        }

        logger.LogInformation("Completed meter value merge for data older than {cutoffDate}", cutoffDate);
    }

    private async Task MergeMeterValueKindAsync(MeterValueKind meterValueKind, DateTimeOffset cutoffDate, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({meterValueKind}, {cutoffDate})", nameof(MergeMeterValueKindAsync), meterValueKind, cutoffDate);

        // Get the date range for processing to avoid loading all data into memory at once
        var dateQuery = await context.MeterValues
            .Where(mv => mv.MeterValueKind == meterValueKind 
                && mv.Timestamp < cutoffDate
                && mv.CarId == null 
                && mv.ChargingConnectorId == null)
            .Select(mv => mv.Timestamp.Date)
            .OrderBy(d => d)
            .FirstOrDefaultAsync(cancellationToken);

        if (dateQuery == default)
        {
            logger.LogDebug("No meter values found for {meterValueKind} older than {cutoffDate}", meterValueKind, cutoffDate);
            return;
        }

        var earliestDate = dateQuery;

        var latestDate = cutoffDate.Date;
        var totalDays = (latestDate - earliestDate).Days + 1;
        var processedDays = 0;
        
        logger.LogInformation("Processing {meterValueKind} data from {earliestDate} to {latestDate} ({totalDays} days)", 
            meterValueKind, earliestDate, latestDate, totalDays);

        // Process data day by day to avoid loading millions of records into memory
        for (var currentDate = earliestDate; currentDate <= latestDate; currentDate = currentDate.AddDays(1))
        {
            await MergeMeterValueKindForDateAsync(meterValueKind, currentDate, cutoffDate, cancellationToken);
            processedDays++;
            
            if (processedDays % 10 == 0 || processedDays == totalDays)
            {
                logger.LogInformation("Processed {processedDays}/{totalDays} days for {meterValueKind}", 
                    processedDays, totalDays, meterValueKind);
            }
        }

        logger.LogInformation("Completed processing {meterValueKind} data for {totalDays} days", meterValueKind, totalDays);
    }

    private async Task MergeMeterValueKindForDateAsync(MeterValueKind meterValueKind, DateTime date, DateTimeOffset cutoffDate, CancellationToken cancellationToken)
    {
        // Load meter values for a single day only to keep memory usage manageable
        var dayStart = new DateTimeOffset(date, TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);

        var dayMeterValues = await context.MeterValues
            .Where(mv => mv.MeterValueKind == meterValueKind 
                && mv.Timestamp >= dayStart 
                && mv.Timestamp < dayEnd
                && mv.Timestamp < cutoffDate
                && mv.CarId == null 
                && mv.ChargingConnectorId == null)
            .OrderBy(mv => mv.Timestamp)
            .ToListAsync(cancellationToken);

        if (dayMeterValues.Count == 0)
        {
            return;
        }

        // Group values into 5-minute windows
        var mergedValues = new List<MeterValue>();
        var valuesToRemove = new List<MeterValue>();

        var currentWindow = DateTimeOffset.MinValue;
        var windowValues = new List<MeterValue>();

        foreach (var meterValue in dayMeterValues)
        {
            var meterValueWindow = GetFiveMinuteWindow(meterValue.Timestamp);

            if (currentWindow != meterValueWindow)
            {
                // Process the previous window if it has values
                if (windowValues.Count > 0)
                {
                    var (mergedValue, toRemove) = CreateMergedValueFromWindow(windowValues);
                    if (mergedValue != null)
                    {
                        mergedValues.Add(mergedValue);
                    }
                    valuesToRemove.AddRange(toRemove);
                }

                // Start a new window
                currentWindow = meterValueWindow;
                windowValues.Clear();
            }

            windowValues.Add(meterValue);
        }

        // Process the last window
        if (windowValues.Count > 0)
        {
            var (mergedValue, toRemove) = CreateMergedValueFromWindow(windowValues);
            if (mergedValue != null)
            {
                mergedValues.Add(mergedValue);
            }
            valuesToRemove.AddRange(toRemove);
        }

        // Apply changes to database
        if (valuesToRemove.Count > 0 || mergedValues.Count > 0)
        {
            logger.LogDebug("Day {date}: Merging {originalCount} values into {mergedCount} for {meterValueKind}, removing {removeCount} duplicates", 
                date, dayMeterValues.Count, mergedValues.Count, meterValueKind, valuesToRemove.Count);

            // Remove duplicate values (keep one representative per window)
            context.MeterValues.RemoveRange(valuesToRemove);

            // Update existing values that were kept
            foreach (var mergedValue in mergedValues)
            {
                if (mergedValue.Id > 0)
                {
                    context.MeterValues.Update(mergedValue);
                }
                else
                {
                    context.MeterValues.Add(mergedValue);
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private DateTimeOffset GetFiveMinuteWindow(DateTimeOffset timestamp)
    {
        // Round down to the nearest 5-minute boundary
        var minutes = timestamp.Minute;
        var roundedMinutes = (minutes / 5) * 5;
        return new DateTimeOffset(timestamp.Year, timestamp.Month, timestamp.Day, 
            timestamp.Hour, roundedMinutes, 0, timestamp.Offset);
    }

    private (MeterValue? mergedValue, List<MeterValue> toRemove) CreateMergedValueFromWindow(List<MeterValue> windowValues)
    {
        if (windowValues.Count <= 1)
        {
            return (windowValues.FirstOrDefault(), new List<MeterValue>());
        }

        // Keep the first value as representative and mark others for removal
        var representativeValue = windowValues.First();
        var toRemove = windowValues.Skip(1).ToList();

        // Calculate averages for the representative value based on all values in the window
        var validPowerValues = windowValues.Where(v => v.MeasuredPower != 0).ToList();
        var validHomeBatteryValues = windowValues.Where(v => v.MeasuredHomeBatteryPower != 0).ToList();
        var validGridPowerValues = windowValues.Where(v => v.MeasuredGridPower != 0).ToList();

        if (validPowerValues.Count > 1)
        {
            representativeValue.MeasuredPower = (int)validPowerValues.Average(v => v.MeasuredPower);
        }

        if (validHomeBatteryValues.Count > 1)
        {
            representativeValue.MeasuredHomeBatteryPower = (int)validHomeBatteryValues.Average(v => v.MeasuredHomeBatteryPower);
        }

        if (validGridPowerValues.Count > 1)
        {
            representativeValue.MeasuredGridPower = (int)validGridPowerValues.Average(v => v.MeasuredGridPower);
        }

        // Get the window boundary timestamp and latest energy value
        var windowBoundary = GetFiveMinuteWindow(representativeValue.Timestamp);
        var latestEnergyValue = windowValues.OrderByDescending(v => v.Timestamp).First();
        
        // Calculate time difference between window boundary and latest timestamp
        var timeDifferenceSeconds = (latestEnergyValue.Timestamp - windowBoundary).TotalSeconds;
        
        // For energy values, adjust them to represent values at the window boundary
        // Energy values are cumulative, so we need to subtract the energy consumed
        // between the window boundary and the latest timestamp
        representativeValue.EstimatedEnergyWs = CalculateEnergyAtWindowBoundary(
            latestEnergyValue.EstimatedEnergyWs, representativeValue.MeasuredPower, timeDifferenceSeconds);
        representativeValue.EstimatedHomeBatteryEnergyWs = CalculateEnergyAtWindowBoundary(
            latestEnergyValue.EstimatedHomeBatteryEnergyWs, representativeValue.MeasuredHomeBatteryPower, timeDifferenceSeconds);
        representativeValue.EstimatedGridEnergyWs = CalculateEnergyAtWindowBoundary(
            latestEnergyValue.EstimatedGridEnergyWs, representativeValue.MeasuredGridPower, timeDifferenceSeconds);

        // Update timestamp to the window boundary
        representativeValue.Timestamp = windowBoundary;

        return (representativeValue, toRemove);
    }

    private long? CalculateEnergyAtWindowBoundary(long? latestEnergyValue, int averagePower, double timeDifferenceSeconds)
    {
        if (latestEnergyValue == null || timeDifferenceSeconds <= 0)
        {
            return latestEnergyValue;
        }

        // Calculate energy consumed between window boundary and latest timestamp
        // Energy = Power * Time (in watt-seconds)
        var energyConsumedSinceWindowBoundary = (long)(averagePower * timeDifferenceSeconds);
        
        // Subtract this energy to get the value that would have been at the window boundary
        return latestEnergyValue.Value - energyConsumedSinceWindowBoundary;
    }
}
