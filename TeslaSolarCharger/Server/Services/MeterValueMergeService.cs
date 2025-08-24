using Microsoft.EntityFrameworkCore;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Helper.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class MeterValueMergeService : IMeterValueMergeService
{
    private readonly ILogger<MeterValueMergeService> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITimestampHelper _timestampHelper;
    private readonly IMeterValueEstimationService _meterValueEstimationService;

    private static readonly TimeSpan NormalizeInterval = TimeSpan.FromMinutes(5);
    private const int MergeValuesOlderThanDays = 21;

    public MeterValueMergeService(ILogger<MeterValueMergeService> logger,
        IDateTimeProvider dateTimeProvider,
        IServiceProvider serviceProvider,
        ITimestampHelper timestampHelper,
        IMeterValueEstimationService meterValueEstimationService)
    {
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
        _serviceProvider = serviceProvider;
        _timestampHelper = timestampHelper;
        _meterValueEstimationService = meterValueEstimationService;
    }

    public async Task MergeOldMeterValuesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("{method}()", nameof(MergeOldMeterValuesAsync));

        var cutoffDate = _dateTimeProvider.DateTimeOffSetUtcNow().AddDays(-MergeValuesOlderThanDays).Date;

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
            await MergeMeterValueKindAsync(meterValueKind, new(cutoffDate, TimeSpan.Zero), cancellationToken);
        }

        _logger.LogInformation("Completed meter value merge for data older than {cutoffDate}", cutoffDate);
    }

    private async Task MergeMeterValueKindAsync(MeterValueKind meterValueKind, DateTimeOffset cutoffDate, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({meterValueKind}, {cutoffDate})", nameof(MergeMeterValueKindAsync), meterValueKind, cutoffDate);
        using var scope = _serviceProvider.CreateScope();
        var outerContext = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
        var firstNotNormalizedTimeStamp = await outerContext.MeterValues
            .Where(mv => mv.MeterValueKind == meterValueKind && mv.NormalizeInterval == null)
            .OrderBy(mv => mv.Timestamp)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (firstNotNormalizedTimeStamp == default || firstNotNormalizedTimeStamp.Timestamp >= cutoffDate)
        {
            return;
        }
        var latestNormalizedTimeStamp = await outerContext.MeterValues
            .Where(mv => mv.MeterValueKind == meterValueKind && mv.NormalizeInterval != null)
            .OrderByDescending(mv => mv.Timestamp)
            .Select(mv => new { mv.Timestamp })
            .FirstOrDefaultAsync(cancellationToken);

        var firstDay = latestNormalizedTimeStamp == default ? firstNotNormalizedTimeStamp.Timestamp.Date
            : latestNormalizedTimeStamp.Timestamp.Date;
        var startDateTimeOffset = new DateTimeOffset(firstDay, TimeSpan.Zero);
        var days = (int)(cutoffDate - startDateTimeOffset).TotalDays;
        for (var i = 0; i < days; i++)
        {
            await MergeMeterValuesForDayAsync(meterValueKind, startDateTimeOffset.AddDays(i), cancellationToken);
        }
    }

    private async Task MergeMeterValuesForDayAsync(MeterValueKind meterValueKind, DateTimeOffset dayToMergeValues, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({meterValueKind}, {dayToMergeValues})", nameof(MergeMeterValuesForDayAsync), meterValueKind, dayToMergeValues);

        var nextDay = dayToMergeValues.AddDays(1);
        var slices = _timestampHelper.GenerateSlicedTimeStamps(dayToMergeValues, nextDay, NormalizeInterval).ToList();

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();

        // Load all meter values for the entire day into memory at once
        var allDayMeterValues = await context.MeterValues
            .Where(mv => mv.MeterValueKind == meterValueKind
                      && mv.Timestamp >= dayToMergeValues
                      && mv.Timestamp <= nextDay)
            .OrderBy(mv => mv.Timestamp)
            .ToListAsync(cancellationToken);

        // Group values by slice for efficient lookup
        var valuesBySlice = allDayMeterValues
            .GroupBy(mv => slices.FirstOrDefault(s => mv.Timestamp >= s && mv.Timestamp < s.Add(NormalizeInterval)))
            .ToDictionary(g => g.Key, g => g.ToList());

        var valuesToAdd = new List<MeterValue>();
        var valuesToUpdate = new List<MeterValue>();
        var valuesToDelete = new List<MeterValue>();

        foreach (var slice in slices)
        {
            var nextSlice = slice.Add(NormalizeInterval);

            // Get values for this slice from in-memory collection
            var sliceValues = valuesBySlice.TryGetValue(slice, out var values)
                ? values
                : new List<MeterValue>();

            var lastMeterValueInSlice = sliceValues
                .Where(mv => mv.Timestamp <= nextSlice)
                .OrderByDescending(mv => mv.Timestamp)
                .FirstOrDefault();

            if (lastMeterValueInSlice?.Timestamp < nextSlice)
            {
                // Create new meter value for next slice start
                var nextSliceStartMeterValue = new MeterValue(nextSlice, meterValueKind, lastMeterValueInSlice.MeasuredPower)
                {
                    MeasuredHomeBatteryPower = lastMeterValueInSlice.MeasuredHomeBatteryPower,
                    MeasuredGridPower = lastMeterValueInSlice.MeasuredGridPower,
                    NormalizeInterval = NormalizeInterval,
                };

                nextSliceStartMeterValue = await _meterValueEstimationService.UpdateMeterValueEstimation(
                    nextSliceStartMeterValue, lastMeterValueInSlice);

                valuesToAdd.Add(nextSliceStartMeterValue);
            }
            else if (lastMeterValueInSlice != default && lastMeterValueInSlice.NormalizeInterval == default)
            {
                lastMeterValueInSlice.NormalizeInterval = NormalizeInterval;
                valuesToUpdate.Add(lastMeterValueInSlice);
            }

            // Collect values to delete (all except the last one in the slice)
            valuesToDelete.AddRange(sliceValues
                .Where(mv => mv.Timestamp > slice && mv.Timestamp < nextSlice));
        }

        // Perform all database operations in batches
        if (valuesToDelete.Any())
        {
            context.MeterValues.RemoveRange(valuesToDelete);
        }

        if (valuesToAdd.Any())
        {
            context.MeterValues.AddRange(valuesToAdd);
        }

        if (valuesToUpdate.Any())
        {
            context.MeterValues.UpdateRange(valuesToUpdate);
        }

        // Single save operation for all changes
        await context.SaveChangesAsync(cancellationToken);
    }
}
