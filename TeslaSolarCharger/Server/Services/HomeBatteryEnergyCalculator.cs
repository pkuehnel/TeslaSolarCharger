using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Resources.Contracts;

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

    public HomeBatteryEnergyCalculator(ILogger<HomeBatteryEnergyCalculator> logger,
        IConfigurationWrapper configurationWrapper,
        IDateTimeProvider dateTimeProvider,
        ISettings settings,
        ISunCalculator sunCalculator,
        IEnergyDataService energyDataService,
        IConstants constants)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
        _dateTimeProvider = dateTimeProvider;
        _settings = settings;
        _sunCalculator = sunCalculator;
        _energyDataService = energyDataService;
        _constants = constants;
    }

    public async Task RefreshHomeBatteryMinSoc(CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}()", nameof(RefreshHomeBatteryMinSoc));
        if (!_configurationWrapper.DynamicHomeBatteryMinSoc())
        {
            return;
        }

        var currentDate = _dateTimeProvider.DateTimeOffSetUtcNow();
        var homeBatteryUsableEnergy = _configurationWrapper.HomeBatteryUsableEnergy();
        if (homeBatteryUsableEnergy == default)
        {
            _logger.LogWarning(
                "Dynamic Home Battery Min SoC is enabled, but no usable energy configured. Using configured home battery min soc.");
            return;
        }

        var calculateMinSoc = await GetDynamicMinSocAtTime(currentDate, homeBatteryUsableEnergy.Value, cancellationToken)
            .ConfigureAwait(false);
        if (calculateMinSoc.HasValue && calculateMinSoc != _configurationWrapper.HomeBatteryMinSoc())
        {
            var configuration = await _configurationWrapper.GetBaseConfigurationAsync();
            configuration.HomeBatteryMinSoc = calculateMinSoc;
            await _configurationWrapper.UpdateBaseConfigurationAsync(configuration);
        }
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

        return await GetDynamicMinSocAtTime(targetTime, homeBatteryUsableEnergy.Value, cancellationToken).ConfigureAwait(false);
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
            futureFullHour,
            schedules);

        return estimatedSoc;
    }

    private async Task<int?> GetDynamicMinSocAtTime(DateTimeOffset targetTime,
        int homeBatteryUsableEnergy, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({targetTime}, {homeBatteryUsableEnergy})", nameof(GetDynamicMinSocAtTime), targetTime,
            homeBatteryUsableEnergy);
        var homeGeofenceLatitude = _configurationWrapper.HomeGeofenceLatitude();
        var homeGeofenceLongitude = _configurationWrapper.HomeGeofenceLongitude();
        var nextSunset = _sunCalculator.NextSunset(homeGeofenceLatitude,
            homeGeofenceLongitude, targetTime, _constants.WeatherPredictionInFutureDays - 1);
        var forceFullBatteryBySunset = _configurationWrapper.ForceFullHomeBatteryBySunset();
        if (nextSunset == default)
        {
            _logger.LogWarning("Could not calculate sunset for current date {targetTime}. Using configured home battery min soc.",
                targetTime);
            return null;
        }

        var nextSunrise = _sunCalculator.NextSunrise(homeGeofenceLatitude, homeGeofenceLongitude, targetTime,
            _constants.WeatherPredictionInFutureDays - 1);
        if (nextSunrise == default)
        {
            _logger.LogWarning("Could not calculate sunrise for current date {targetTime}. Using configured home battery min soc.",
                targetTime);
            return null;
        }

        _settings.NextSunEvent = nextSunrise < nextSunset ? NextSunEvent.Sunrise : NextSunEvent.Sunset;
        var targetDate = nextSunrise.Value;
        var isTargetDateSunrise = true;
        if (forceFullBatteryBySunset && _settings.NextSunEvent == NextSunEvent.Sunset)
        {
            targetDate = nextSunset.Value;
            isTargetDateSunrise = false;
        }

        _logger.LogTrace("Next sunrise: {nextSunrise}", nextSunrise);
        _logger.LogTrace("Next sunset: {nextSunset}", nextSunset);

        var predictionInterval = TimeSpan.FromHours(1);
        // Make sure battery does not run out the next day
        var targetDateFullHour =
            new DateTimeOffset(targetDate.Year, targetDate.Month, targetDate.Day, targetDate.Hour, 0, 0, TimeSpan.Zero);
        //Get surplus until next day +2 hours because rounding down hours in line before (+1 hour required) + if days get longer sunrise of next day can be in the next hour (+ another hour required)
        var getSurplusSlicesUntil = targetDateFullHour.AddHours(26);
        var currentNextFullHour = targetTime.NextFullHour();
        var predictedSurplusPerSlices = await _energyDataService
            .GetPredictedSurplusPerSlice(currentNextFullHour, getSurplusSlicesUntil, predictionInterval, cancellationToken)
            .ConfigureAwait(false);
        //If target date is sunrise iterate over all surplusses after sunrise until there is a positive surplus
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

        var calculateMinSoc = CalculateRequiredInitialStateOfChargePercent(
            predictedSurplusPerSlices, homeBatteryUsableEnergy,
            _configurationWrapper.HomeBatteryMinDynamicMinSoc(),
            isTargetDateSunrise ? _configurationWrapper.HomeBatteryMinDynamicMinSoc() : _configurationWrapper.HomeBatteryMaxDynamicMinSoc(),
            targetDateFullHour,
            _configurationWrapper.DynamicMinSocCalculationBufferInPercent());
        if (calculateMinSoc > _configurationWrapper.HomeBatteryMaxDynamicMinSoc())
        {
            calculateMinSoc = _configurationWrapper.HomeBatteryMaxDynamicMinSoc();
        }

        return calculateMinSoc;
    }


    /// <summary>
    /// Calculates the required initial state-of-charge (SOC) percentage so that:
    /// 1. The battery never drops below <paramref name="minimalStateOfChargePercent"/>% SOC during the series of hourly energy differences.
    /// 2. The battery ends at <paramref name="targetStateOfChargePercent"/>% SOC after processing all hourly differences.
    /// </summary>
    /// <param name="energyDifferences">
    ///     A mapping from timestamp to net energy (Wh) produced by the solar system
    ///     minus the house consumption in that hour.</param>
    /// <param name="batteryUsableCapacityInWh">
    ///     The total usable capacity of the home battery in Wh.
    /// </param>
    /// <param name="minimalStateOfChargePercent">
    ///     The minimum SOC percentage (e.g. 5 means 5%).
    /// </param>
    /// <param name="targetStateOfChargePercent">
    ///     The target end SOC percentage (e.g. 95 means 95%).
    /// </param>
    /// <param name="targetTime"></param>
    /// <param name="dynamicMinSocCalculationBufferInPercent"></param>
    /// <returns>
    /// The required initial SOC expressed in% between 0 and 100.
    /// </returns>
    private int CalculateRequiredInitialStateOfChargePercent(IReadOnlyDictionary<DateTimeOffset, int> energyDifferences,
        int batteryUsableCapacityInWh,
        int minimalStateOfChargePercent,
        int targetStateOfChargePercent,
        DateTimeOffset targetTime,
        int dynamicMinSocCalculationBufferInPercent)
    {
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
                return minimalStateOfChargePercent;
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
            return minimalStateOfChargePercent;
        }

        var requiredInitialSoc = (double)(minimumEnergy + finalMissingEnergy) / batteryUsableCapacityInWh;
        _logger.LogDebug("Required initial SoC: {requiredInitialSoc:P2}", requiredInitialSoc);
        return (int)(requiredInitialSoc * 100);
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

            if (intervalStart > targetTime)
            {
                break;
            }

            // Determine the end of this time interval
            var intervalEnd = (i + 1 < sortedEntries.Count) ? sortedEntries[i + 1].Key : targetTime;

            if (intervalEnd > targetTime)
            {
                intervalEnd = targetTime;
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
            var adjustedEnergyDifference = currentEntry.Value - consumedBySchedulesWh;

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
        if (energyDifference > 0 && batteryMaxChargingPower.HasValue && energyDifference > batteryMaxChargingPower.Value)
        {
            _logger.LogTrace("Use max charging power");
            return currentEnergyInBattery + batteryMaxChargingPower.Value;
        }

        _logger.LogTrace("Use actual additional energy.");
        return currentEnergyInBattery + energyDifference;
    }
}
