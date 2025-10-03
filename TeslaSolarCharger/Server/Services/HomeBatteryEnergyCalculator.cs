using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.Settings;
using TeslaSolarCharger.Shared.Resources.Contracts;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
        var homeBatteryUsableEnergy = _configurationWrapper.HomeBatteryUsableEnergy();
        var currentDate = _dateTimeProvider.DateTimeOffSetUtcNow();
        if (homeBatteryUsableEnergy == default)
        {
            _logger.LogWarning("Dynamic Home Battery Min SoC is enabled, but no usable energy configured. Using configured home battery min soc.");
            return;
        }

        var homeGeofenceLatitude = _configurationWrapper.HomeGeofenceLatitude();
        var homeGeofenceLongitude = _configurationWrapper.HomeGeofenceLongitude();
        var nextSunset = _sunCalculator.NextSunset(homeGeofenceLatitude,
            homeGeofenceLongitude, currentDate, _constants.WeatherPredictionInFutureDays - 1);
        var forceFullBatteryBySunset = _configurationWrapper.ForceFullHomeBatteryBySunset();
        if (nextSunset == default)
        {
            _logger.LogWarning("Could not calculate sunset for current date {currentDate}. Using configured home battery min soc.", currentDate);
            return;
        }
        var nextSunrise = _sunCalculator.NextSunrise(homeGeofenceLatitude, homeGeofenceLongitude, currentDate, _constants.WeatherPredictionInFutureDays - 1);
        if (nextSunrise == default)
        {
            _logger.LogWarning("Could not calculate sunrise for current date {currentDate}. Using configured home battery min soc.", currentDate);
            return;
        }
        var targetDate = nextSunrise.Value;
        var isTargetDateSunrise = true;
        if (forceFullBatteryBySunset)
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
        var hour = currentDate.Hour + 1;
        var day = currentDate.Day;
        if (hour >= 24)
        {
            hour -= 24;
            day += 1;
        }
        var currentNextFullHour = new DateTimeOffset(currentDate.Year, currentDate.Month, day, hour, 0, 0, currentDate.Offset);
        var predictedSurplusPerSlices = await _energyDataService.GetPredictedSurplusPerSlice(currentNextFullHour, getSurplusSlicesUntil, predictionInterval, cancellationToken).ConfigureAwait(false);
        _settings.HomeBatteryTargetSocBasedOn = isTargetDateSunrise ? HomeBatteryTargetSocBasedOn.Sunrise : HomeBatteryTargetSocBasedOn.Sunset;
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
            predictedSurplusPerSlices, homeBatteryUsableEnergy.Value,
            _configurationWrapper.HomeBatteryMinDynamicMinSoc(),
            isTargetDateSunrise ? _configurationWrapper.HomeBatteryMinDynamicMinSoc() : _configurationWrapper.HomeBatteryMaxDynamicMinSoc(),
            targetDateFullHour,
            _configurationWrapper.DynamicMinSocCalculationBufferInPercent());
        if (calculateMinSoc > _configurationWrapper.HomeBatteryMaxDynamicMinSoc())
        {
            calculateMinSoc = _configurationWrapper.HomeBatteryMaxDynamicMinSoc();
        }
        if (calculateMinSoc != _configurationWrapper.HomeBatteryMinSoc())
        {
            var configuration = await _configurationWrapper.GetBaseConfigurationAsync();
            configuration.HomeBatteryMinSoc = calculateMinSoc;
            await _configurationWrapper.UpdateBaseConfigurationAsync(configuration);
        }
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
        var minimumEnergy = (int)(batteryUsableCapacityInWh * (minimalStateOfChargePercent / 100.0));
        var targetEnergy = (int)(batteryUsableCapacityInWh * (targetStateOfChargePercent / 100.0));
        var maxMissingEnergy = 0;
        var energyInBattery = minimumEnergy;
        var localDictionary = energyDifferences.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
        var closestDistanceToMaxEnergy = batteryUsableCapacityInWh - energyInBattery;
        var batteryMaxChargingPower = _configurationWrapper.HomeBatteryChargingPower();

        // NEW: track energy at (or just before) targetTime
        var energyAtTargetTime = energyInBattery;

        foreach (var energyDifference in localDictionary)
        {
            if (energyDifference.Value > batteryMaxChargingPower)
            {
                energyInBattery += batteryMaxChargingPower.Value;
            }
            else
            {
                energyInBattery += energyDifference.Value;
            }

            if (energyDifference.Key <= targetTime)
            {
                energyAtTargetTime = energyInBattery;
                //Only set closest distance to max energy until target time as otherwise values after sunrise are taken into account
                closestDistanceToMaxEnergy = Math.Min(closestDistanceToMaxEnergy, batteryUsableCapacityInWh - energyInBattery);
            }

            
            if (energyInBattery > batteryUsableCapacityInWh)
            {
                _logger.LogDebug("Energy in battery exceeds capacity at {Time}: {EnergyInBattery} Wh. MinSoc higher than minimum would not help.", energyDifference.Key, energyInBattery);
                return minimalStateOfChargePercent;
            }
            var missingEnergy = minimumEnergy - energyInBattery;
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
}
