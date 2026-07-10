using TeslaSolarCharger.Server.Dtos.HomeBatteryControl;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.SignalR.Notifiers.Contracts;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Resources.Contracts;
using TeslaSolarCharger.Shared.SignalRClients;

namespace TeslaSolarCharger.Server.Services;

public class HomeBatteryEnergyCalculator : IHomeBatteryEnergyCalculator
{
    private readonly ILogger<HomeBatteryEnergyCalculator> _logger;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISettings _settings;
    private readonly ISunCalculator _sunCalculator;
    private readonly IEnergyDataService _energyDataService;
    private readonly IConstants _constants;
    private readonly IAppStateNotifier _appStateNotifier;

    public HomeBatteryEnergyCalculator(ILogger<HomeBatteryEnergyCalculator> logger,
        IConfigurationWrapper configurationWrapper,
        IDateTimeProvider dateTimeProvider,
        ISettings settings,
        ISunCalculator sunCalculator,
        IEnergyDataService energyDataService,
        IConstants constants,
        IAppStateNotifier appStateNotifier)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
        _dateTimeProvider = dateTimeProvider;
        _settings = settings;
        _sunCalculator = sunCalculator;
        _energyDataService = energyDataService;
        _constants = constants;
        _appStateNotifier = appStateNotifier;
    }

    public async Task RefreshHomeBatteryMinSoc(CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}()", nameof(RefreshHomeBatteryMinSoc));

        var currentDate = _dateTimeProvider.DateTimeOffSetUtcNow();
        var (nextSunrise, nextSunset, nextSunEvent) = GetNextSunEvent(currentDate);
        _settings.NextSunEvent = nextSunEvent;
        _logger.LogTrace("Updated {settingsName}.{nextSunEventName} to {nextSunEventValue}", nameof(ISettings), nameof(ISettings.NextSunEvent), nextSunEvent);

        var homeBatteryUsableEnergy = _configurationWrapper.HomeBatteryUsableEnergy();

        var canComputeDynamicSocTargets = homeBatteryUsableEnergy != default
                                          && nextSunrise != default
                                          && nextSunset != default;

        if (!canComputeDynamicSocTargets)
        {
            if (_configurationWrapper.DynamicHomeBatteryMinSoc())
            {
                _logger.LogWarning("Dynamic Home Battery Min SoC (or related targets) requested but usable energy or sun events are not available.");
            }
            return;
        }

        //The min soc, hold and charge targets all use the same prediction data, so only fetch it once
        var (predictedSurplusPerSlices, selfSufficiencyTime, isTargetDateSunrise) = await GetSurplusPredictionData(currentDate,
            nextSunrise!.Value, nextSunset!.Value, nextSunEvent, cancellationToken).ConfigureAwait(false);
        var minDynamicSoc = _configurationWrapper.HomeBatteryMinDynamicMinSoc();
        var maxDynamicSoc = _configurationWrapper.HomeBatteryMaxDynamicMinSoc();

        if (_configurationWrapper.DynamicHomeBatteryMinSoc())
        {
            var minSocResult = CalculateDynamicBatteryTargetSoc(predictedSurplusPerSlices, selfSufficiencyTime, isTargetDateSunrise,
                homeBatteryUsableEnergy!.Value, minDynamicSoc, maxDynamicSoc,
                _configurationWrapper.DynamicMinSocCalculationBufferInPercent());
            if (minSocResult.RequiredInitialSocPercent != _configurationWrapper.HomeBatteryMinSoc())
            {
                var configuration = await _configurationWrapper.GetBaseConfigurationAsync();
                configuration.HomeBatteryMinSoc = minSocResult.RequiredInitialSocPercent;
                await _configurationWrapper.UpdateBaseConfigurationAsync(configuration);
                var changes = new StateUpdateDto()
                {
                    DataType = DataTypeConstants.DynamicHomeBatteryMinSocChangeTrigger,
                    Timestamp = _dateTimeProvider.DateTimeOffSetUtcNow(),
                };
                await _appStateNotifier.NotifyStateUpdateAsync(changes).ConfigureAwait(false);
            }
        }

        var holdTarget = CalculateDynamicBatteryTargetSoc(predictedSurplusPerSlices, selfSufficiencyTime, isTargetDateSunrise,
            homeBatteryUsableEnergy!.Value, minDynamicSoc, maxDynamicSoc,
            _configurationWrapper.HoldHomeBatteryChargeSocBufferInPercent());
        _settings.HomeBatteryHoldTarget = holdTarget;
        _logger.LogTrace("Hold target: SOC={soc}%, firstBreach={breach}, additionalWh={wh}, selfSufficientAt={selfSufficiencyTime}",
            holdTarget.RequiredInitialSocPercent, holdTarget.FirstBreachTime, holdTarget.AdditionalEnergyRequiredWh, holdTarget.SelfSufficiencyTime);

        var chargeTarget = CalculateDynamicBatteryTargetSoc(predictedSurplusPerSlices, selfSufficiencyTime, isTargetDateSunrise,
            homeBatteryUsableEnergy.Value, minDynamicSoc, maxDynamicSoc,
            _configurationWrapper.ChargeHomeBatterySocBufferInPercent());
        _settings.HomeBatteryChargeTarget = chargeTarget;
        _logger.LogTrace("Charge target: SOC={soc}%, firstBreach={breach}, additionalWh={wh}, selfSufficientAt={selfSufficiencyTime}",
            chargeTarget.RequiredInitialSocPercent, chargeTarget.FirstBreachTime, chargeTarget.AdditionalEnergyRequiredWh, chargeTarget.SelfSufficiencyTime);
    }

    public async Task<int?> GetHomeBatteryMinSocAtTime(DateTimeOffset targetTime, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({targetTime})", nameof(GetHomeBatteryMinSocAtTime), targetTime);
        if (!_configurationWrapper.DynamicHomeBatteryMinSoc())
        {
            _logger.LogTrace("Dynamic Home Battery Min SoC is disabled. Using configured home battery min soc.");
            return _configurationWrapper.HomeBatteryMinSoc();
        }

        var homeBatteryUsableEnergy = _configurationWrapper.HomeBatteryUsableEnergy();
        if (homeBatteryUsableEnergy == default)
        {
            _logger.LogWarning(
                "Dynamic Home Battery Min SoC is enabled, but no usable energy configured. Using configured home battery min soc.");
            return _configurationWrapper.HomeBatteryMinSoc();
        }
        var (nextSunrise, nextSunset, nextSunEvent) = GetNextSunEvent(targetTime);
        if (nextSunrise == default || nextSunset == default)
        {
            return null;
        }

        return await GetDynamicMinSocAtTime(targetTime, homeBatteryUsableEnergy.Value, nextSunrise.Value, nextSunset.Value, nextSunEvent, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DtoHomeBatterySurplusPrediction?> GetSurplusPrediction(DateTimeOffset targetTime, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({targetTime})", nameof(GetSurplusPrediction), targetTime);
        var (nextSunrise, nextSunset, nextSunEvent) = GetNextSunEvent(targetTime);
        if (nextSunrise == default || nextSunset == default)
        {
            _logger.LogWarning("Can not create surplus prediction as sun events are unknown.");
            return null;
        }
        var (predictedSurplusPerSlices, selfSufficiencyTime, isTargetDateSunrise) = await GetSurplusPredictionData(targetTime,
            nextSunrise.Value, nextSunset.Value, nextSunEvent, cancellationToken).ConfigureAwait(false);
        return new DtoHomeBatterySurplusPrediction(predictedSurplusPerSlices, selfSufficiencyTime, isTargetDateSunrise);
    }

    /// <summary>
    /// Estimates the home battery state of charge at a future time based on predicted energy surpluses.
    /// </summary>
    /// <param name="futureTime">The future time to estimate SoC for</param>
    /// <param name="currentSocPercent">The current actual battery SoC percentage</param>
    /// <param name="schedules"></param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The estimated SoC percentage at the future time, or null if calculation fails</returns>
    public async Task<int?> GetEstimatedHomeBatterySocAtTime(DateTimeOffset futureTime, int currentSocPercent,
        List<DtoChargingSchedule> schedules,
        CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({futureTime}, {currentSocPercent}, {@schedules})", nameof(GetEstimatedHomeBatterySocAtTime), futureTime,
            currentSocPercent, schedules);

        var homeBatteryUsableEnergy = _configurationWrapper.HomeBatteryUsableEnergy();
        if (homeBatteryUsableEnergy == default)
        {
            _logger.LogWarning("No usable energy configured for home battery. Cannot estimate future SoC.");
            return null;
        }

        var currentTime = _dateTimeProvider.DateTimeOffSetUtcNow();
        if (futureTime <= currentTime)
        {
            _logger.LogWarning("Future time {futureTime} is not in the future. Current time: {currentTime}", futureTime, currentTime);
            return currentSocPercent;
        }

        var predictionInterval = TimeSpan.FromHours(1);
        var currentNextFullHour = currentTime.NextFullHour();
        var futureFullHour = new DateTimeOffset(futureTime.Year, futureTime.Month, futureTime.Day, futureTime.Hour, 0, 0, TimeSpan.Zero);
        futureFullHour = futureFullHour.AddHours(1);

        var predictedSurplusPerSlices = await _energyDataService.GetPredictedSurplusPerSlice(
            currentNextFullHour,
            futureFullHour.AddHours(1),
            predictionInterval,
            cancellationToken).ConfigureAwait(false);

        var estimatedSoc = SimulateBatterySoc(
            predictedSurplusPerSlices,
            homeBatteryUsableEnergy.Value,
            currentSocPercent,
            futureTime,
            schedules);

        return estimatedSoc;
    }

    private async Task<int> GetDynamicMinSocAtTime(DateTimeOffset targetTime,
        int homeBatteryUsableEnergy, DateTimeOffset nextSunrise, DateTimeOffset nextSunset, NextSunEvent nextSunEvent, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({targetTime}, {homeBatteryUsableEnergy})", nameof(GetDynamicMinSocAtTime), targetTime,
            homeBatteryUsableEnergy);

        var result = await CalculateDynamicBatteryTargetSoc(
            targetTime,
            homeBatteryUsableEnergy,
            nextSunrise,
            nextSunset,
            nextSunEvent,
            _configurationWrapper.HomeBatteryMinDynamicMinSoc(),
            _configurationWrapper.HomeBatteryMaxDynamicMinSoc(),
            _configurationWrapper.DynamicMinSocCalculationBufferInPercent(),
            cancellationToken).ConfigureAwait(false);
        return result.RequiredInitialSocPercent;
    }

    internal async Task<DtoHomeBatterySocTarget> CalculateDynamicBatteryTargetSoc(DateTimeOffset targetTime,
        int homeBatteryUsableEnergy,
        DateTimeOffset nextSunrise,
        DateTimeOffset nextSunset,
        NextSunEvent nextSunEvent,
        int minimalStateOfChargePercent,
        int targetStateOfChargePercentForSunsetCase,
        int bufferInPercent,
        CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({targetTime}, {homeBatteryUsableEnergy}, buffer={buffer})", nameof(CalculateDynamicBatteryTargetSoc), targetTime, homeBatteryUsableEnergy, bufferInPercent);

        var (predictedSurplusPerSlices, selfSufficiencyTime, isTargetDateSunrise) = await GetSurplusPredictionData(targetTime,
            nextSunrise, nextSunset, nextSunEvent, cancellationToken).ConfigureAwait(false);
        return CalculateDynamicBatteryTargetSoc(predictedSurplusPerSlices, selfSufficiencyTime, isTargetDateSunrise,
            homeBatteryUsableEnergy, minimalStateOfChargePercent, targetStateOfChargePercentForSunsetCase, bufferInPercent);
    }

    /// <summary>
    /// Determines the self sufficiency time (sunrise adjusted to the first positive surplus, or sunset when the
    /// battery should be full by sunset) and fetches the predicted surplus slices covering it.
    /// </summary>
    internal async Task<(Dictionary<DateTimeOffset, int> PredictedSurplusPerSlices, DateTimeOffset SelfSufficiencyTime, bool IsTargetDateSunrise)> GetSurplusPredictionData(
        DateTimeOffset targetTime,
        DateTimeOffset nextSunrise,
        DateTimeOffset nextSunset,
        NextSunEvent nextSunEvent,
        CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({targetTime}, {nextSunrise}, {nextSunset}, {nextSunEvent})", nameof(GetSurplusPredictionData), targetTime, nextSunrise, nextSunset, nextSunEvent);

        var targetDate = nextSunrise;
        var isTargetDateSunrise = true;
        var forceFullBatteryBySunset = _configurationWrapper.ForceFullHomeBatteryBySunset();
        if (forceFullBatteryBySunset && nextSunEvent == NextSunEvent.Sunset)
        {
            targetDate = nextSunset;
            isTargetDateSunrise = false;
        }

        var predictionInterval = TimeSpan.FromHours(1);
        var targetDateFullHour =
            new DateTimeOffset(targetDate.Year, targetDate.Month, targetDate.Day, targetDate.Hour, 0, 0, TimeSpan.Zero);
        var getSurplusSlicesUntil = targetDateFullHour.AddHours(26);
        var currentNextFullHour = targetTime.NextFullHour();
        var predictedSurplusPerSlices = await _energyDataService
            .GetPredictedSurplusPerSlice(currentNextFullHour, getSurplusSlicesUntil, predictionInterval, cancellationToken)
            .ConfigureAwait(false);

        if (isTargetDateSunrise)
        {
            _logger.LogTrace("As target date {targetDate} is sunrise update target date until first positive surplus", targetDateFullHour);
            while (targetDateFullHour < getSurplusSlicesUntil)
            {
                targetDateFullHour = targetDateFullHour.AddHours(1);
                if (!predictedSurplusPerSlices.TryGetValue(targetDateFullHour, out var value))
                {
                    _logger.LogWarning("Could not find target date {targetDate} in predicted surpluses", targetDateFullHour);
                    break;
                }

                if (value > 0)
                {
                    _logger.LogTrace("First positive value {value} found at {targetDate}", value, targetDateFullHour);
                    break;
                }

                _logger.LogTrace("Value {value} for {targetDate} is negative, waiting for positive value", value, targetDateFullHour);
            }
        }

        return (predictedSurplusPerSlices, targetDateFullHour, isTargetDateSunrise);
    }

    internal DtoHomeBatterySocTarget CalculateDynamicBatteryTargetSoc(IReadOnlyDictionary<DateTimeOffset, int> predictedSurplusPerSlices,
        DateTimeOffset selfSufficiencyTime,
        bool isTargetDateSunrise,
        int homeBatteryUsableEnergy,
        int minimalStateOfChargePercent,
        int targetStateOfChargePercentForSunsetCase,
        int bufferInPercent)
    {
        var result = CalculateRequiredInitialStateOfChargePercent(
            predictedSurplusPerSlices,
            homeBatteryUsableEnergy,
            minimalStateOfChargePercent,
            isTargetDateSunrise ? minimalStateOfChargePercent : targetStateOfChargePercentForSunsetCase,
            selfSufficiencyTime,
            bufferInPercent);

        var maxDynamic = _configurationWrapper.HomeBatteryMaxDynamicMinSoc();
        if (result.RequiredInitialSocPercent > maxDynamic)
        {
            result.RequiredInitialSocPercent = maxDynamic;
        }

        return result;
    }

    private (DateTimeOffset? nextSunrise, DateTimeOffset? nextSunset, NextSunEvent nextSunEvent) GetNextSunEvent(DateTimeOffset targetTime)
    {
        var homeGeofenceLatitude = _configurationWrapper.HomeGeofenceLatitude();
        var homeGeofenceLongitude = _configurationWrapper.HomeGeofenceLongitude();
        var nextSunset = _sunCalculator.NextSunset(homeGeofenceLatitude,
            homeGeofenceLongitude, targetTime, _constants.WeatherPredictionInFutureDays - 1);
        if (nextSunset == default)
        {
            _logger.LogWarning("Could not calculate sunset for current date {targetTime}.",
                targetTime);
        }

        var nextSunrise = _sunCalculator.NextSunrise(homeGeofenceLatitude, homeGeofenceLongitude, targetTime,
            _constants.WeatherPredictionInFutureDays - 1);
        if (nextSunrise == default)
        {
            _logger.LogWarning("Could not calculate sunrise for current date {targetTime}.",
                targetTime);
        }
        _logger.LogTrace("Next sunrise: {nextSunrise}", nextSunrise);
        _logger.LogTrace("Next sunset: {nextSunset}", nextSunset);

        if (nextSunrise == default || nextSunset == default)
        {
            return (nextSunrise, nextSunset, NextSunEvent.Unknown);
        }

        var nextSunEvent = nextSunrise < nextSunset ? NextSunEvent.Sunrise : NextSunEvent.Sunset;
        return (nextSunrise, nextSunset, nextSunEvent);
    }


    /// <summary>
    /// Calculates the required initial state-of-charge (SOC) percentage so that:
    /// 1. The battery never drops below <paramref name="minimalStateOfChargePercent"/>% SOC during the series of hourly energy differences.
    /// 2. The battery ends at <paramref name="targetStateOfChargePercent"/>% SOC after processing all hourly differences.
    /// Also returns diagnostic info for mode scheduling: the first time the floor-start simulation breaches the minimum, and the additional energy (Wh) required.
    /// </summary>
    /// <returns>
    /// Result containing the required initial SOC and breach/deficit data for scheduling Hold/Charge modes.
    /// </returns>
    internal DtoHomeBatterySocTarget CalculateRequiredInitialStateOfChargePercent(IReadOnlyDictionary<DateTimeOffset, int> energyDifferences,
        int batteryUsableCapacityInWh,
        int minimalStateOfChargePercent,
        int targetStateOfChargePercent,
        DateTimeOffset targetTime,
        int dynamicMinSocCalculationBufferInPercent)
    {
        DtoHomeBatterySocTarget CreateResult(int requiredInitialSocPercent, DateTimeOffset? breachTime, int additionalEnergyRequiredWh) => new()
        {
            RequiredInitialSocPercent = requiredInitialSocPercent,
            FirstBreachTime = breachTime,
            AdditionalEnergyRequiredWh = additionalEnergyRequiredWh,
            SelfSufficiencyTime = targetTime,
            CalculatedAt = _dateTimeProvider.DateTimeOffSetUtcNow(),
        };

        _logger.LogTrace(
            "{method}({@energyDifferences}, {batteryUsableCapacityInWh}, {minimalStateOfChargePercent}, {targetStateOfChargePercent}, {targetTime}, {dynamicMinSocCalculationBufferInPercent})",
            nameof(CalculateRequiredInitialStateOfChargePercent), energyDifferences, batteryUsableCapacityInWh, minimalStateOfChargePercent,
            targetStateOfChargePercent, targetTime, dynamicMinSocCalculationBufferInPercent);
        var minimumEnergy = (int)(batteryUsableCapacityInWh * (minimalStateOfChargePercent / 100.0));
        var targetEnergy = (int)(batteryUsableCapacityInWh * (targetStateOfChargePercent / 100.0));
        var maxMissingEnergy = 0;
        var energyInBattery = minimumEnergy;
        var localDictionary = energyDifferences.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
        var closestDistanceToMaxEnergy = batteryUsableCapacityInWh - energyInBattery;
        var batteryMaxChargingPower = _configurationWrapper.HomeBatteryChargingPower();

        var energyAtTargetTime = energyInBattery;
        DateTimeOffset? firstBreachTime = null;

        foreach (var energyDifference in localDictionary)
        {
            _logger.LogTrace("Adding {energy} Wh of {date}", energyDifference.Value, energyDifference.Key);
            energyInBattery = ApplyEnergyChange(energyInBattery, energyDifference.Value, batteryMaxChargingPower);
            _logger.LogTrace("Energy in battery at {date}: {energy} Wh", energyDifference.Key, energyInBattery);
            if (energyDifference.Key <= targetTime)
            {
                energyAtTargetTime = energyInBattery;
                //Only set closest distance to max energy until target time as otherwise values after sunrise are taken into account
                closestDistanceToMaxEnergy = Math.Min(closestDistanceToMaxEnergy, batteryUsableCapacityInWh - energyInBattery);
                _logger.LogTrace("Updated closest distance to max energy to: {closestDistanceToMaxEnergy} Wh", closestDistanceToMaxEnergy);
            }

            if (energyInBattery > batteryUsableCapacityInWh && energyDifference.Key < targetTime)
            {
                _logger.LogDebug(
                    "Energy in battery exceeds capacity at {Time}: {EnergyInBattery} Wh. MinSoc higher than minimum would not help.",
                    energyDifference.Key, energyInBattery);
                return CreateResult(minimalStateOfChargePercent, null, 0);
            }

            var missingEnergy = minimumEnergy - energyInBattery;
            _logger.LogTrace("Missing energy: {missingEnergy} Wh", missingEnergy);
            if (missingEnergy > 0)
            {
                _logger.LogDebug("Missing energy at {Time}: {MissingEnergy} Wh", energyDifference.Key, missingEnergy);
                if (missingEnergy > maxMissingEnergy)
                {
                    maxMissingEnergy = missingEnergy;
                }
                if (!firstBreachTime.HasValue)
                {
                    firstBreachTime = energyDifference.Key;
                }
            }
        }

        _logger.LogDebug("Maximum missing energy: {MaxMissingEnergy} Wh", maxMissingEnergy);

        // CHANGED: ensure target SoC is reached by targetTime
        if (targetEnergy > energyAtTargetTime)
        {
            _logger.LogDebug(
                "At minimum min soc by {TargetTime} target energy of {TargetEnergy} Wh would not be reached. Actual energy: {ActualEnergy}",
                targetTime, targetEnergy, energyAtTargetTime);
            maxMissingEnergy = Math.Max(maxMissingEnergy, targetEnergy - energyAtTargetTime);
        }

        var bufferFactor = (dynamicMinSocCalculationBufferInPercent / (float)100) + 1;
        _logger.LogTrace("Using buffer factor {bufferFactor} for missing energy calculation", bufferFactor);
        _logger.LogTrace("Closest distance to max energy: {closestDistanceToMaxSoc} Wh", closestDistanceToMaxEnergy);
        _logger.LogTrace("Max missing energy: {maxMissingEnergy}", maxMissingEnergy);

        var finalMissingEnergy = Math.Min(closestDistanceToMaxEnergy, maxMissingEnergy) * bufferFactor;
        _logger.LogTrace("Final missing energy after buffer: {finalMissingEnergy} Wh", finalMissingEnergy);

        if (finalMissingEnergy < 0)
        {
            return CreateResult(minimalStateOfChargePercent, null, 0);
        }

        var requiredInitialSoc = (double)(minimumEnergy + finalMissingEnergy) / batteryUsableCapacityInWh;
        _logger.LogDebug("Required initial SoC: {requiredInitialSoc:P2}", requiredInitialSoc);
        return CreateResult((int)(requiredInitialSoc * 100), firstBreachTime, (int)finalMissingEnergy);
    }


    /// <summary>
    /// Simulates battery SoC forward in time based on predicted energy surpluses.
    /// </summary>
    private int SimulateBatterySoc(IReadOnlyDictionary<DateTimeOffset, int> energyDifferences,
    int batteryUsableCapacityInWh,
    int initialSocPercent,
    DateTimeOffset targetTime,
    List<DtoChargingSchedule> schedules)
    {
        _logger.LogTrace("{method}({@energyDifferences}, {batteryUsableCapacityInWh}, {initialSocPercent}, {targetTime}, {@scheduled})",
            nameof(SimulateBatterySoc), energyDifferences, batteryUsableCapacityInWh, initialSocPercent, targetTime, schedules);

        var energyInBattery = (int)(batteryUsableCapacityInWh * (initialSocPercent / 100.0));
        var batteryMaxChargingPower = _configurationWrapper.HomeBatteryChargingPower();

        var sortedEntries = energyDifferences.OrderBy(x => x.Key).ToList();

        for (var i = 0; i < sortedEntries.Count; i++)
        {
            var currentEntry = sortedEntries[i];
            var intervalStart = currentEntry.Key;

            if (intervalStart >= targetTime)
            {
                break;
            }

            // Determine the end of this time interval
            var intervalEnd = (i + 1 < sortedEntries.Count) ? sortedEntries[i + 1].Key : targetTime;

            var energyInThisInterval = currentEntry.Value;
            if (intervalEnd > targetTime)
            {
                var durationBeforeAdjustment = intervalEnd - intervalStart;
                intervalEnd = targetTime;
                var durationAfterAdjustment = intervalEnd - intervalStart;
                energyInThisInterval = (int)(((double)energyInThisInterval) *
                                             (durationAfterAdjustment.TotalSeconds / durationBeforeAdjustment.TotalSeconds));
                _logger.LogTrace("Updated energy from {energyBefore} to {energyAfter} for interval start {intervalStart}", currentEntry.Value, energyInThisInterval, intervalStart);
            }

            // 1. Filter for potentially relevant schedules (optimization)
            var activeSchedules = schedules
                .Where(s => s.ValidFrom < intervalEnd && s.ValidTo > intervalStart)
                .ToList();

            double totalConsumedWh = 0;

            // 2. Calculate specific overlap for each schedule individually
            foreach (var schedule in activeSchedules)
            {
                // The overlap starts at the later of the two start times
                var overlapStart = schedule.ValidFrom > intervalStart ? schedule.ValidFrom : intervalStart;

                // The overlap ends at the earlier of the two end times
                var overlapEnd = schedule.ValidTo < intervalEnd ? schedule.ValidTo : intervalEnd;

                var overlapDuration = overlapEnd - overlapStart;

                // Only calculate if there is a positive duration
                if (overlapDuration.TotalHours > 0)
                {
                    var scheduleEnergy = schedule.EstimatedChargingPower * overlapDuration.TotalHours;
                    totalConsumedWh += scheduleEnergy;

                    _logger.LogTrace("Schedule {id} consumes {energy} Wh ({power}W for {min} min) within interval",
                        schedule.CarId, scheduleEnergy, schedule.EstimatedChargingPower, overlapDuration.TotalMinutes);
                }
            }

            var consumedBySchedulesWh = (int)totalConsumedWh;

            if (consumedBySchedulesWh > 0)
            {
                _logger.LogTrace("Total reduced available energy by {consumed} Wh in interval {start} to {end}",
                   consumedBySchedulesWh, intervalStart, intervalEnd);
            }

            // Adjust the base energy difference
            var adjustedEnergyDifference = energyInThisInterval - consumedBySchedulesWh;

            energyInBattery = ApplyEnergyChange(energyInBattery, adjustedEnergyDifference, batteryMaxChargingPower);

            // Clamp to battery capacity
            energyInBattery = Math.Max(0, Math.Min(energyInBattery, batteryUsableCapacityInWh));

            _logger.LogTrace("Energy in battery at {date}: {energy} Wh", intervalStart, energyInBattery);
        }

        var finalSocPercent = (int)((energyInBattery / (double)batteryUsableCapacityInWh) * 100);
        _logger.LogDebug("Estimated SoC at {targetTime}: {finalSocPercent}%", targetTime, finalSocPercent);

        return finalSocPercent;
    }

    /// <summary>
    /// Applies energy change to battery, respecting charging power limits.
    /// </summary>
    private int ApplyEnergyChange(int currentEnergyInBattery, int energyDifference, int? batteryMaxChargingPower)
    {
        _logger.LogTrace("{method}({currentEnergyInBattery}, {energyDifference}, {batteryMaxChargingPower})", nameof(ApplyEnergyChange), currentEnergyInBattery, energyDifference, batteryMaxChargingPower);
        if (energyDifference > 0 && batteryMaxChargingPower.HasValue && energyDifference > batteryMaxChargingPower.Value)
        {
            _logger.LogTrace("Use max charging power");
            return currentEnergyInBattery + batteryMaxChargingPower.Value;
        }

        _logger.LogTrace("Use actual additional energy.");
        return currentEnergyInBattery + energyDifference;
    }
}
