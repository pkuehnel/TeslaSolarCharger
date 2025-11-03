using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class MeterValueMergeJob(
    ILogger<MeterValueMergeJob> logger, 
    IMeterValueMergeService meterValueMergeService) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await meterValueMergeService.MergeOldMeterValuesAsync(context.CancellationToken).ConfigureAwait(false);
    }
}
