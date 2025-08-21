namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IMeterValueMergeService
{
    /// <summary>
    /// Merges meter values older than the specified number of days into 5-minute intervals.
    /// Only processes meter values that are not related to cars or charging stations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task MergeOldMeterValuesAsync(CancellationToken cancellationToken = default);
}
