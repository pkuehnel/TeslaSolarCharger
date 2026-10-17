using Microsoft.EntityFrameworkCore;
using System.Globalization;
using TeslaSolarCharger.Model.Contracts;
using TeslaSolarCharger.Model.Entities.TeslaSolarCharger;
using TeslaSolarCharger.Model.Enums;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.Services.SolarValueGathering.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.UsableEnergy;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services;

/// <summary>
/// Learns the usable energy of cars and the home battery from past charges: the energy that moved between two SoC
/// readings, scaled to 100 %. The learned value is only used while it is based on enough consistent samples, the
/// entered value is never changed.
/// </summary>
public class UsableEnergyLearningService(
    ILogger<UsableEnergyLearningService> logger,
    ITeslaSolarChargerContext context,
    IConfigurationWrapper configurationWrapper,
    ISettings settings,
    IDateTimeProvider dateTimeProvider,
    ITscConfigurationService tscConfigurationService,
    IGenericValueService genericValueService,
    IConstants constants) : IUsableEnergyLearningService
{
    //Below this SoC difference the integer SoC makes a sample too imprecise (1 % of 20 % is already 5 %).
    internal const int MinSocDifference = 20;
    internal const int RequiredSampleCount = 5;
    internal const int MaxSampleCount = 30;
    internal const double MaxSpreadPercent = 15;
    internal const int MaxOppositeEnergyFlowPercent = 5;
    internal const int MaxSocStep = 5;
    internal static readonly TimeSpan MaxMeterValueGap = TimeSpan.FromMinutes(15);
    internal const int MinCarUsableEnergyWh = 10_000;
    internal const int MaxCarUsableEnergyWh = 250_000;
    internal const int MinHomeBatteryUsableEnergyWh = 1_000;
    internal const int MaxHomeBatteryUsableEnergyWh = 200_000;
    private const int ShownSampleCount = 50;
    //Car meter values before this date were deleted as invalid, see MeterValueEstimationService.
    private static readonly DateTime CarMeterHistoryStart = new(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly UsableEnergySampleKind[] HomeBatteryKinds =
        [UsableEnergySampleKind.HomeBatteryCharge, UsableEnergySampleKind.HomeBatteryDischarge,];

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}()", nameof(RefreshAsync));
        await AddCarSamples(cancellationToken).ConfigureAwait(false);
        if (HasMultipleHomeBatteries())
        {
            logger.LogTrace("Not learning the home battery usable energy as several home batteries report a SoC.");
        }
        else
        {
            await AddHomeBatterySamples(cancellationToken).ConfigureAwait(false);
        }
        await UpdateLearnedValuesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateLearnedValuesAsync(CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}()", nameof(UpdateLearnedValuesAsync));
        var carIds = await context.Cars.Select(c => c.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var carId in carIds)
        {
            var learnedWh = (await GetCarStateAsync(carId, cancellationToken).ConfigureAwait(false)).LearnedUsableEnergyKwh * 1000;
            if (learnedWh == null)
            {
                settings.LearnedCarUsableEnergiesWh.TryRemove(carId, out _);
            }
            else
            {
                settings.LearnedCarUsableEnergiesWh[carId] = (int)Math.Round(learnedWh.Value);
            }
        }
        foreach (var removedCarId in settings.LearnedCarUsableEnergiesWh.Keys.Except(carIds).ToList())
        {
            settings.LearnedCarUsableEnergiesWh.TryRemove(removedCarId, out _);
        }
        var homeBatteryLearnedKwh = (await GetHomeBatteryStateAsync(cancellationToken).ConfigureAwait(false)).LearnedUsableEnergyKwh;
        settings.LearnedHomeBatteryUsableEnergyWh = homeBatteryLearnedKwh == null ? null : (int)Math.Round(homeBatteryLearnedKwh.Value * 1000);
        logger.LogTrace("Learned car usable energies: {@cars}, home battery: {homeBattery}", settings.LearnedCarUsableEnergiesWh, settings.LearnedHomeBatteryUsableEnergyWh);
    }

    public async Task<DtoLearnedUsableEnergy> GetCarStateAsync(int carId, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({carId})", nameof(GetCarStateAsync), carId);
        var samples = await context.UsableEnergySamples
            .Where(s => s.CarId == carId && s.Kind == UsableEnergySampleKind.CarCharge)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return BuildState(samples, [UsableEnergySampleKind.CarCharge,], false);
    }

    public async Task<DtoLearnedUsableEnergy> GetHomeBatteryStateAsync(CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}()", nameof(GetHomeBatteryStateAsync));
        var samples = await context.UsableEnergySamples
            .Where(s => s.CarId == null && HomeBatteryKinds.Contains(s.Kind))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return BuildState(samples, HomeBatteryKinds, HasMultipleHomeBatteries());
    }

    public async Task ResetCarAsync(int carId, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({carId})", nameof(ResetCarAsync), carId);
        await ForgetCarSamples(carId, cancellationToken).ConfigureAwait(false);
        await UpdateLearnedValuesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ResetHomeBatteryAsync(CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}()", nameof(ResetHomeBatteryAsync));
        await context.UsableEnergySamples
            .Where(s => s.CarId == null && HomeBatteryKinds.Contains(s.Kind))
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await SetHomeBatteryLearnedUntil(dateTimeProvider.DateTimeOffSetUtcNow()).ConfigureAwait(false);
        await UpdateLearnedValuesAsync(cancellationToken).ConfigureAwait(false);
    }

    private DtoLearnedUsableEnergy BuildState(List<UsableEnergySample> samples, UsableEnergySampleKind[] kinds, bool hasMultipleHomeBatteries)
    {
        var chargeLossPercent = configurationWrapper.CarChargeLoss();
        var evaluatedSamples = samples
            .OrderByDescending(s => s.EndTime)
            .Select(s => EvaluateSample(s, chargeLossPercent))
            .ToList();
        var estimates = new List<DtoUsableEnergyEstimate>();
        foreach (var kind in kinds)
        {
            var usedSamples = evaluatedSamples
                .Where(s => s.Kind == kind && s.RejectionReason == null)
                .Take(MaxSampleCount)
                .ToList();
            usedSamples.ForEach(s => s.IsUsedForEstimate = true);
            estimates.Add(Estimate(kind, usedSamples.Select(s => s.ImpliedUsableEnergyKwh!.Value * 1000).ToList()));
        }
        //Charging and discharging each miss part of the truth (loss on the way in, loss on the way out), so a home
        //battery is only learned once both directions are reliable, as their average.
        var learnedKwh = !hasMultipleHomeBatteries && estimates.All(e => e.IsReliable)
            ? estimates.Average(e => e.MedianKwh!.Value)
            : (double?)null;
        var learningEnabled = configurationWrapper.UseLearnedUsableEnergy();
        return new DtoLearnedUsableEnergy
        {
            LearningEnabled = learningEnabled,
            HasMultipleHomeBatteries = hasMultipleHomeBatteries,
            LearnedUsableEnergyKwh = learnedKwh == null ? null : Math.Round(learnedKwh.Value, 1),
            IsUsed = learningEnabled && learnedKwh != null,
            Estimates = estimates,
            Samples = evaluatedSamples.Take(ShownSampleCount).ToList(),
        };
    }

    /// <summary>
    /// The charge loss is applied here rather than when the sample is stored, so a changed charge loss setting also
    /// applies to old samples.
    /// </summary>
    internal static DtoUsableEnergySample EvaluateSample(UsableEnergySample sample, int chargeLossPercent)
    {
        var impliedWh = GetImpliedUsableEnergyWh(sample, chargeLossPercent);
        var rejectionReason = sample.RejectionReason;
        if (rejectionReason == null && (impliedWh == null || !IsPlausible(sample.Kind, impliedWh.Value)))
        {
            rejectionReason = UsableEnergySampleRejectionReason.ImplausibleCapacity;
        }
        return new DtoUsableEnergySample
        {
            Kind = sample.Kind,
            StartTime = sample.StartTime,
            EndTime = sample.EndTime,
            StartSoc = sample.StartSoc,
            EndSoc = sample.EndSoc,
            //Every other stored rejection is decided before the energy is measured.
            EnergyKwh = sample.RejectionReason is null or UsableEnergySampleRejectionReason.OppositeEnergyFlow
                ? Math.Round(sample.EnergyWh / 1000.0, 2)
                : null,
            ImpliedUsableEnergyKwh = sample.RejectionReason == null && impliedWh != null ? Math.Round(impliedWh.Value / 1000.0, 2) : null,
            RejectionReason = rejectionReason,
        };
    }

    internal static double? GetImpliedUsableEnergyWh(UsableEnergySample sample, int chargeLossPercent)
    {
        var socDifference = Math.Abs(sample.EndSoc - sample.StartSoc);
        if (socDifference == 0)
        {
            return null;
        }
        double energyWh = sample.EnergyWh;
        if (sample.Kind == UsableEnergySampleKind.CarCharge)
        {
            //The meter counts the energy taken from the wall, the usable energy is what ends up in the battery.
            energyWh /= 1 + (chargeLossPercent / 100.0);
        }
        return energyWh * 100 / socDifference;
    }

    internal static bool IsPlausible(UsableEnergySampleKind kind, double impliedUsableEnergyWh)
    {
        return kind == UsableEnergySampleKind.CarCharge
            ? impliedUsableEnergyWh is >= MinCarUsableEnergyWh and <= MaxCarUsableEnergyWh
            : impliedUsableEnergyWh is >= MinHomeBatteryUsableEnergyWh and <= MaxHomeBatteryUsableEnergyWh;
    }

    /// <summary>
    /// Median of the newest accepted samples. It only counts as reliable with enough samples whose middle half lies
    /// close together, so a few outliers (preconditioning while charging, a cold battery) do not move it.
    /// </summary>
    internal static DtoUsableEnergyEstimate Estimate(UsableEnergySampleKind kind, IReadOnlyList<double> impliedUsableEnergiesWh)
    {
        var estimate = new DtoUsableEnergyEstimate
        {
            Kind = kind,
            AcceptedSampleCount = impliedUsableEnergiesWh.Count,
            RequiredSampleCount = RequiredSampleCount,
            MaxSpreadPercent = MaxSpreadPercent,
        };
        if (impliedUsableEnergiesWh.Count == 0)
        {
            return estimate;
        }
        var sorted = impliedUsableEnergiesWh.Order().ToList();
        var median = Percentile(sorted, 50);
        var spreadPercent = (Percentile(sorted, 75) - Percentile(sorted, 25)) / median * 100;
        estimate.MedianKwh = Math.Round(median / 1000, 2);
        estimate.SpreadPercent = Math.Round(spreadPercent, 1);
        estimate.IsReliable = sorted.Count >= RequiredSampleCount && spreadPercent <= MaxSpreadPercent;
        return estimate;
    }

    /// <summary>Linear interpolation between the closest ranks.</summary>
    internal static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        var rank = percentile / 100 * (sortedValues.Count - 1);
        var lowerIndex = (int)Math.Floor(rank);
        var upperIndex = (int)Math.Ceiling(rank);
        return sortedValues[lowerIndex] + ((sortedValues[upperIndex] - sortedValues[lowerIndex]) * (rank - lowerIndex));
    }

    private bool HasMultipleHomeBatteries()
    {
        return genericValueService.GetSourceValues(false)
            .Where(v => v.UsedFor == ValueUsage.HomeBatterySoc)
            .Select(v => new { v.ConfigurationType, v.SourceId, })
            .Distinct()
            .Count() > 1;
    }

    private async Task AddCarSamples(CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}()", nameof(AddCarSamples));
        var cars = await context.Cars
            .Select(c => new { c.Id, c.Vin, c.UsableEnergyLearnedSince, })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var car in cars)
        {
            var learnedSince = car.UsableEnergyLearnedSince;
            if (car.Vin != null
                && await context.UsableEnergySamples.AnyAsync(s => s.CarId == car.Id && s.CarVin != null && s.CarVin != car.Vin, cancellationToken).ConfigureAwait(false))
            {
                logger.LogInformation("Car {carId} has a different VIN than when its usable energy was learned, so it is learned again.", car.Id);
                learnedSince = await ForgetCarSamples(car.Id, cancellationToken).ConfigureAwait(false);
            }
            var firstConsideredStart = learnedSince > CarMeterHistoryStart ? learnedSince.Value : CarMeterHistoryStart;
            var chargingProcesses = await context.ChargingProcesses
                .Where(p => p.CarId == car.Id
                            && p.EndDate != null
                            && p.StartDate >= firstConsideredStart
                            && !context.UsableEnergySamples.Any(s => s.ChargingProcessId == p.Id))
                .OrderBy(p => p.StartDate)
                .Select(p => new { p.Id, p.StartDate, EndDate = p.EndDate!.Value, })
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            logger.LogTrace("Creating usable energy samples of {count} charging processes of car {carId}", chargingProcesses.Count, car.Id);
            foreach (var chargingProcess in chargingProcesses)
            {
                var sample = await CreateCarSample(car.Id, car.Vin, chargingProcess.Id, chargingProcess.StartDate, chargingProcess.EndDate, cancellationToken).ConfigureAwait(false);
                context.UsableEnergySamples.Add(sample);
            }
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <returns>The point in time from which on the car is learned again.</returns>
    private async Task<DateTime> ForgetCarSamples(int carId, CancellationToken cancellationToken)
    {
        await context.UsableEnergySamples
            .Where(s => s.CarId == carId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        var learnedSince = dateTimeProvider.UtcNow();
        await context.Cars
            .Where(c => c.Id == carId)
            .ExecuteUpdateAsync(c => c.SetProperty(car => car.UsableEnergyLearnedSince, learnedSince), cancellationToken).ConfigureAwait(false);
        return learnedSince;
    }

    internal async Task<UsableEnergySample> CreateCarSample(int carId, string? vin, int chargingProcessId, DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        var sample = new UsableEnergySample
        {
            Kind = UsableEnergySampleKind.CarCharge,
            CarId = carId,
            CarVin = vin,
            ChargingProcessId = chargingProcessId,
            StartTime = new DateTimeOffset(start, TimeSpan.Zero),
            EndTime = new DateTimeOffset(end, TimeSpan.Zero),
        };
        var socReadings = (await context.CarValueLogs
                .Where(l => l.CarId == carId
                            && l.Type == CarValueType.StateOfCharge
                            && l.Source > CarValueSource.Estimation
                            && l.Timestamp >= start
                            && l.Timestamp <= end)
                .OrderBy(l => l.Timestamp)
                .Select(l => new { l.Timestamp, l.IntValue, l.DoubleValue, })
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .Where(l => l.IntValue != null || l.DoubleValue != null)
            .Select(l => (Timestamp: new DateTimeOffset(l.Timestamp, TimeSpan.Zero), Soc: l.IntValue ?? Convert.ToInt32(l.DoubleValue!.Value)))
            .ToList();
        if (socReadings.Count < 2)
        {
            sample.RejectionReason = UsableEnergySampleRejectionReason.MissingSoc;
            return sample;
        }
        var first = socReadings.First();
        var last = socReadings.Last();
        sample.StartTime = first.Timestamp;
        sample.EndTime = last.Timestamp;
        sample.StartSoc = first.Soc;
        sample.EndSoc = last.Soc;
        if (last.Soc - first.Soc < MinSocDifference)
        {
            sample.RejectionReason = UsableEnergySampleRejectionReason.SocDifferenceTooSmall;
            return sample;
        }
        var startCounter = await GetEnergyCounterAt(MeterValueKind.Car, carId, first.Timestamp, cancellationToken).ConfigureAwait(false);
        var endCounter = await GetEnergyCounterAt(MeterValueKind.Car, carId, last.Timestamp, cancellationToken).ConfigureAwait(false);
        if (startCounter == null || endCounter == null)
        {
            sample.RejectionReason = UsableEnergySampleRejectionReason.MissingMeterValues;
            return sample;
        }
        sample.EnergyWh = (int)((endCounter.Value - startCounter.Value) / 3600);
        return sample;
    }

    private async Task AddHomeBatterySamples(CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}()", nameof(AddHomeBatterySamples));
        var learnedUntil = await GetHomeBatteryLearnedUntil().ConfigureAwait(false);
        var socLogs = (await context.PvValueLogs
                .Where(l => l.Type == PvValueType.HomeBatterySoc && (learnedUntil == null || l.Timestamp >= learnedUntil))
                .OrderBy(l => l.Timestamp)
                .Select(l => new { l.Timestamp, l.IntValue, })
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .Select(l => (l.Timestamp, Soc: l.IntValue))
            .ToList();
        var (closedRuns, openRunStart) = SplitIntoRuns(socLogs);
        logger.LogTrace("Found {count} finished home battery SoC runs since {learnedUntil}", closedRuns.Count, learnedUntil);
        foreach (var run in closedRuns.Where(r => Math.Abs(r[^1].Soc - r[0].Soc) >= MinSocDifference))
        {
            var sample = await CreateHomeBatterySample(run, cancellationToken).ConfigureAwait(false);
            context.UsableEnergySamples.Add(sample);
        }
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (openRunStart != null)
        {
            await SetHomeBatteryLearnedUntil(openRunStart.Value).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Splits SoC readings into runs of only rising or only falling SoC. A run ends at the reading where the SoC turns
    /// around, which is also where the next run starts. The last run is still open as the SoC might keep moving in the
    /// same direction.
    /// </summary>
    internal static (List<List<(DateTimeOffset Timestamp, int Soc)>> ClosedRuns, DateTimeOffset? OpenRunStart) SplitIntoRuns(
        List<(DateTimeOffset Timestamp, int Soc)> socReadings)
    {
        var closedRuns = new List<List<(DateTimeOffset Timestamp, int Soc)>>();
        if (socReadings.Count == 0)
        {
            return (closedRuns, null);
        }
        var runStartIndex = 0;
        var direction = 0;
        for (var i = 1; i < socReadings.Count; i++)
        {
            var currentDirection = Math.Sign(socReadings[i].Soc - socReadings[i - 1].Soc);
            if (currentDirection == 0)
            {
                continue;
            }
            if (direction != 0 && currentDirection != direction)
            {
                closedRuns.Add(socReadings.GetRange(runStartIndex, i - runStartIndex));
                runStartIndex = i - 1;
            }
            direction = currentDirection;
        }
        return (closedRuns, socReadings[runStartIndex].Timestamp);
    }

    internal async Task<UsableEnergySample> CreateHomeBatterySample(List<(DateTimeOffset Timestamp, int Soc)> run, CancellationToken cancellationToken)
    {
        var start = run[0];
        var end = run[^1];
        var isCharge = end.Soc > start.Soc;
        var sample = new UsableEnergySample
        {
            Kind = isCharge ? UsableEnergySampleKind.HomeBatteryCharge : UsableEnergySampleKind.HomeBatteryDischarge,
            StartTime = start.Timestamp,
            EndTime = end.Timestamp,
            StartSoc = start.Soc,
            EndSoc = end.Soc,
        };
        for (var i = 1; i < run.Count; i++)
        {
            if (Math.Abs(run[i].Soc - run[i - 1].Soc) > MaxSocStep)
            {
                sample.RejectionReason = UsableEnergySampleRejectionReason.SocJump;
                return sample;
            }
        }
        var chargedStart = await GetEnergyCounterAt(MeterValueKind.HomeBatteryCharging, null, start.Timestamp, cancellationToken).ConfigureAwait(false);
        var chargedEnd = await GetEnergyCounterAt(MeterValueKind.HomeBatteryCharging, null, end.Timestamp, cancellationToken).ConfigureAwait(false);
        var dischargedStart = await GetEnergyCounterAt(MeterValueKind.HomeBatteryDischarging, null, start.Timestamp, cancellationToken).ConfigureAwait(false);
        var dischargedEnd = await GetEnergyCounterAt(MeterValueKind.HomeBatteryDischarging, null, end.Timestamp, cancellationToken).ConfigureAwait(false);
        if (chargedStart == null || chargedEnd == null || dischargedStart == null || dischargedEnd == null)
        {
            sample.RejectionReason = UsableEnergySampleRejectionReason.MissingMeterValues;
            return sample;
        }
        if (await HasMeterValueGap(MeterValueKind.HomeBatteryCharging, start.Timestamp, end.Timestamp, cancellationToken).ConfigureAwait(false))
        {
            sample.RejectionReason = UsableEnergySampleRejectionReason.MeterValueGap;
            return sample;
        }
        var chargedWh = (chargedEnd.Value - chargedStart.Value) / 3600;
        var dischargedWh = (dischargedEnd.Value - dischargedStart.Value) / 3600;
        var forwardWh = isCharge ? chargedWh : dischargedWh;
        var oppositeWh = isCharge ? dischargedWh : chargedWh;
        sample.EnergyWh = (int)(forwardWh - oppositeWh);
        if (oppositeWh * 100 > forwardWh * MaxOppositeEnergyFlowPercent)
        {
            sample.RejectionReason = UsableEnergySampleRejectionReason.OppositeEnergyFlow;
        }
        return sample;
    }

    private async Task<bool> HasMeterValueGap(MeterValueKind kind, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        var timestamps = await context.MeterValues
            .Where(m => m.MeterValueKind == kind && m.CarId == null && m.ChargingConnectorId == null
                        && m.Timestamp >= start && m.Timestamp <= end)
            .OrderBy(m => m.Timestamp)
            .Select(m => m.Timestamp)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        timestamps.Insert(0, start);
        timestamps.Add(end);
        for (var i = 1; i < timestamps.Count; i++)
        {
            if (timestamps[i] - timestamps[i - 1] > MaxMeterValueGap)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// The value of the cumulative energy counter at <paramref name="timestamp"/> in Ws. Each meter value's power applies
    /// to the time since the previous meter value, so the counter is exact between two meter values.
    /// </summary>
    internal async Task<long?> GetEnergyCounterAt(MeterValueKind kind, int? carId, DateTimeOffset timestamp, CancellationToken cancellationToken)
    {
        var previous = await context.MeterValues
            .Where(m => m.MeterValueKind == kind && m.CarId == carId && m.ChargingConnectorId == null
                        && m.EstimatedEnergyWs != null && m.Timestamp <= timestamp)
            .OrderByDescending(m => m.Timestamp)
            .Select(m => new { m.Timestamp, m.EstimatedEnergyWs, m.MeasuredPower, })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (previous == null)
        {
            return null;
        }
        var next = await context.MeterValues
            .Where(m => m.MeterValueKind == kind && m.CarId == carId && m.ChargingConnectorId == null
                        && m.Timestamp > timestamp)
            .OrderBy(m => m.Timestamp)
            .Select(m => new { m.MeasuredPower, })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (next == null)
        {
            //Nothing is metered after a zero power value until the next one, so the counter stays where it is.
            return previous.MeasuredPower == 0 ? previous.EstimatedEnergyWs : null;
        }
        return previous.EstimatedEnergyWs + (long)(next.MeasuredPower * (timestamp - previous.Timestamp).TotalSeconds);
    }

    private async Task<DateTimeOffset?> GetHomeBatteryLearnedUntil()
    {
        var value = await tscConfigurationService.GetConfigurationValueByKey(constants.HomeBatteryUsableEnergyLearnedUntil).ConfigureAwait(false);
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var learnedUntil) ? learnedUntil : null;
    }

    private Task SetHomeBatteryLearnedUntil(DateTimeOffset learnedUntil)
    {
        return tscConfigurationService.SetConfigurationValueByKey(constants.HomeBatteryUsableEnergyLearnedUntil,
            learnedUntil.ToString("O", CultureInfo.InvariantCulture));
    }
}
