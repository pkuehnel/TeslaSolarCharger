using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class MeterValueMergeJob(
    ILogger<MeterValueMergeJob> logger, 
    IMeterValueMergeService meterValueMergeService) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await meterValueMergeService.MergeOldMeterValuesAsync(cancellationToken).ConfigureAwait(false);
    }
}
