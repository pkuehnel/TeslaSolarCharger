using TeslaSolarCharger.Model.Enums;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IMeterValueMergeService
{
    /// <summary>
    /// Merges meter values older than the specified number of days into 5-minute intervals.
    /// Only processes meter values that are not related to cars or charging stations.
    /// </summary>
    /// <param name="olderThanDays">Number of days to look back from current date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    Task MergeOldMeterValuesAsync(int olderThanDays, CancellationToken cancellationToken = default);
}