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
        var slices = _timestampHelper.GenerateSlicedTimeStamps(dayToMergeValues, nextDay, NormalizeInterval);
        foreach (var slice in slices)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ITeslaSolarChargerContext>();
            var nextSlice = slice.Add(NormalizeInterval);
            var lastMeterValueInSlice = await context.MeterValues
                .Where(mv => mv.MeterValueKind == meterValueKind && mv.Timestamp >= slice && mv.Timestamp <= nextSlice)
                .OrderByDescending(mv => mv.Timestamp)
                .FirstOrDefaultAsync(cancellationToken);
            if (lastMeterValueInSlice?.Timestamp < nextSlice)
            {
                var nextSliceStartMeterValue = new MeterValue(nextSlice, meterValueKind, lastMeterValueInSlice.MeasuredPower)
                {
                    MeasuredHomeBatteryPower = lastMeterValueInSlice.MeasuredHomeBatteryPower,
                    MeasuredGridPower = lastMeterValueInSlice.MeasuredGridPower,
                    NormalizeInterval = NormalizeInterval,
                };
                nextSliceStartMeterValue = await _meterValueEstimationService.UpdateMeterValueEstimation(nextSliceStartMeterValue, lastMeterValueInSlice);
                context.MeterValues.Add(nextSliceStartMeterValue);
                await context.SaveChangesAsync(cancellationToken);
            }
            else if (lastMeterValueInSlice != default && lastMeterValueInSlice.NormalizeInterval == default)
            {
                lastMeterValueInSlice.NormalizeInterval = NormalizeInterval;
                await context.SaveChangesAsync(cancellationToken);
            }
            await context.MeterValues
                .Where(mv => mv.MeterValueKind == meterValueKind
                        && mv.Timestamp > slice
                        && mv.Timestamp < nextSlice)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}
