using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class HomeBatteryEnergyCalculator(ILogger<HomeBatteryEnergyCalculator> logger, IConfigurationWrapper configurationWrapper) : IHomeBatteryEnergyCalculator
{
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
    public int CalculateRequiredInitialStateOfChargeFraction(
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
        var batteryMaxChargingPower = configurationWrapper.HomeBatteryChargingPower();
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
                logger.LogDebug("Energy in battery exceeds capacity at {Time}: {EnergyInBattery} Wh. MinSoc higher than minimum would not help.", energyDifference.Key, energyInBattery);
                return minimalStateOfChargePercent;
            }
            var missingEnergy = minimumEnergy - energyInBattery;
            if (missingEnergy > 0)
            {
                logger.LogDebug("Missing energy at {Time}: {MissingEnergy} Wh", energyDifference.Key, missingEnergy);
                if (missingEnergy > maxMissingEnergy)
                {
                    maxMissingEnergy = missingEnergy;
                }
            }
        }
        logger.LogDebug("Maximum missing energy: {MaxMissingEnergy} Wh", maxMissingEnergy);
        if (targetEnergy > energyInBattery)
        {
            logger.LogDebug("At minimum min soc after expected energy differences target energy of {targetEnergy} Wh would not be reached. Actual energy: {acutalEnergy}", targetEnergy, energyInBattery);
            maxMissingEnergy = Math.Max(maxMissingEnergy, targetEnergy - energyInBattery);
        }
        var finalMissingEnergy = Math.Min(closestDistanceToMaxEnergy, maxMissingEnergy);
        if (finalMissingEnergy < 0)
        {
            return minimalStateOfChargePercent;
        }
        var requiredInitialSoc = (double)(minimumEnergy + finalMissingEnergy) / batteryUsableCapacityInWh;
        return (int)requiredInitialSoc * 100;
    }
}
