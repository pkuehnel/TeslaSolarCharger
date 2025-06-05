using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class HomeBatteryEnergyCalculator : IHomeBatteryEnergyCalculator
{
    private readonly ILogger<HomeBatteryEnergyCalculator> _logger;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISettings _settings;
    private readonly ISunCalculator _sunCalculator;
    private readonly IEnergyDataService _energyDataService;

    public HomeBatteryEnergyCalculator(ILogger<HomeBatteryEnergyCalculator> logger,
        IConfigurationWrapper configurationWrapper,
        IDateTimeProvider dateTimeProvider,
        ISettings settings,
        ISunCalculator sunCalculator,
        IEnergyDataService energyDataService)
    {
        _logger = logger;
        _configurationWrapper = configurationWrapper;
        _dateTimeProvider = dateTimeProvider;
        _settings = settings;
        _sunCalculator = sunCalculator;
        _energyDataService = energyDataService;
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
        var currentHomeBatterySoc = _settings.HomeBatterySoc;
        if (currentHomeBatterySoc == default)
        {
            _logger.LogWarning("Dynamic Home Battery Min SoC is enabled, bur current Soc is unknown.");
            return;
        }

        var homeGeofenceLatitude = _configurationWrapper.HomeGeofenceLatitude();
        var homeGeofenceLongitude = _configurationWrapper.HomeGeofenceLongitude();
        var nextSunset = _sunCalculator.CalculateSunset(homeGeofenceLatitude,
            homeGeofenceLongitude, currentDate);
        if (nextSunset < currentDate)
        {
            nextSunset = _sunCalculator.CalculateSunset(homeGeofenceLatitude,
                homeGeofenceLongitude, currentDate.AddDays(1));
        }
        if (nextSunset == default)
        {
            _logger.LogWarning("Could not calculate sunrise for current date {currentDate}. Using configured home battery min soc.", currentDate);
            return;
        }
        //Do not try to fully charge to allow some buffer with fast reaction time compared to cars.
        var fullBatterySoc = 95;
        var requiredEnergyForFullBattery = (int)(homeBatteryUsableEnergy.Value * ((fullBatterySoc - currentHomeBatterySoc.Value) / 100.0m));
        if (requiredEnergyForFullBattery < 1)
        {
            _logger.LogDebug("No energy required to charge home battery to full.");
            return;
        }
        var predictionInterval = TimeSpan.FromHours(1);
        var fullBatteryTargetTime = new DateTimeOffset(nextSunset.Value.Year, nextSunset.Value.Month, nextSunset.Value.Day,
            nextSunset.Value.Hour + 1, 0, 0, nextSunset.Value.Offset);
        var currentNextFullHour = new DateTimeOffset(currentDate.Year, currentDate.Month, currentDate.Day, currentDate.Hour + 1, 0, 0, currentDate.Offset);
        var predictedSurplusPerSlices = await _energyDataService.GetPredictedSurplusPerSlice(currentNextFullHour, fullBatteryTargetTime, predictionInterval, cancellationToken).ConfigureAwait(false);
        var calculateMinSoc = CalculateRequiredInitialStateOfChargeFraction(
            predictedSurplusPerSlices, homeBatteryUsableEnergy.Value, 5, fullBatterySoc);
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
    /// A mapping from timestamp to net energy (Wh) produced by the solar system
    /// minus the house consumption in that hour.</param>
    /// <param name="batteryUsableCapacityInWh">
    /// The total usable capacity of the home battery in Wh.
    /// </param>
    /// <param name="minimalStateOfChargePercent">
    /// The minimum SOC percentage (e.g. 5 means 5%).
    /// </param>
    /// <param name="targetStateOfChargePercent">
    /// The target end SOC percentage (e.g. 95 means 95%).
    /// </param>
    /// <returns>
    /// The required initial SOC expressed as a fraction between 0.0 and 1.0.
    /// </returns>
    private int CalculateRequiredInitialStateOfChargeFraction(
        IReadOnlyDictionary<DateTimeOffset, int> energyDifferences,
        int batteryUsableCapacityInWh,
        int minimalStateOfChargePercent,
        int targetStateOfChargePercent
    )
    {
        var minimumEnergy = (int)(batteryUsableCapacityInWh * (minimalStateOfChargePercent / 100.0));
        var targetEnergy = (int)(batteryUsableCapacityInWh * (targetStateOfChargePercent / 100.0));
        var maxMissingEnergy = 0;
        var energyInBattery = minimumEnergy;
        var localDictionary = energyDifferences.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
        var closestDistanceToMaxEnergy = batteryUsableCapacityInWh - energyInBattery;
        var batteryMaxChargingPower = _configurationWrapper.HomeBatteryChargingPower();
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
            closestDistanceToMaxEnergy = Math.Min(closestDistanceToMaxEnergy, batteryUsableCapacityInWh - energyInBattery);
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
        if (targetEnergy > energyInBattery)
        {
            _logger.LogDebug("At minimum min soc after expected energy differences target energy of {targetEnergy} Wh would not be reached. Actual energy: {acutalEnergy}", targetEnergy, energyInBattery);
            maxMissingEnergy = Math.Max(maxMissingEnergy, targetEnergy - energyInBattery);
        }
        var finalMissingEnergy = Math.Min(closestDistanceToMaxEnergy, maxMissingEnergy);
        if (finalMissingEnergy < 0)
        {
            return minimalStateOfChargePercent;
        }
        var requiredInitialSoc = (double)(minimumEnergy + finalMissingEnergy) / batteryUsableCapacityInWh;
        return (int)(requiredInitialSoc * 100);
    }
}
