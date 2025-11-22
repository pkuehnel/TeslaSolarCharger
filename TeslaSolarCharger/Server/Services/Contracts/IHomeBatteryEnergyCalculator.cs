namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IHomeBatteryEnergyCalculator
{
    Task RefreshHomeBatteryMinSoc(CancellationToken cancellationToken);
    Task<int?> GetHomeBatteryMinSocAtTime(DateTimeOffset targetTime, CancellationToken cancellationToken);

    /// <summary>
    /// Estimates the home battery state of charge at a future time based on predicted energy surpluses.
    /// </summary>
    /// <param name="futureTime">The future time to estimate SoC for</param>
    /// <param name="currentSocPercent">The current actual battery SoC percentage</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The estimated SoC percentage at the future time, or null if calculation fails</returns>
    Task<int?> GetEstimatedHomeBatterySocAtTime(DateTimeOffset futureTime, int currentSocPercent, CancellationToken cancellationToken);
}
