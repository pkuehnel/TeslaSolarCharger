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
        
        // Use the same constant as EnergyDataService for consistency
        const int historicPredictionsSearchDaysBeforePredictionStart = 21;
        
        await meterValueMergeService.MergeOldMeterValuesAsync(context.CancellationToken).ConfigureAwait(false);
    }
}