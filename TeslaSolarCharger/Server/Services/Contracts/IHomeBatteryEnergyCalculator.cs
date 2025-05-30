namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IHomeBatteryEnergyCalculator
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
    int CalculateRequiredInitialStateOfChargeFraction(
        IReadOnlyDictionary<DateTimeOffset, int> energyDifferences,
        int batteryUsableCapacityInWh,
        int minimalStateOfChargePercent,
        int targetStateOfChargePercent
    );
}
