using Quartz;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class MeterValueEstimationJob(ILogger<MeterValueEstimationJob> logger, IMeterValueEstimationService service) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await service.UpdateEstimatedMeterValues().ConfigureAwait(false);
    }
}
