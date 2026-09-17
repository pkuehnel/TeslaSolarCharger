using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class TeslaMateChargeCostUpdateJob(ILogger<TeslaMateChargeCostUpdateJob> logger, ITeslaMateChargeCostUpdateService service) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await service.UpdateTeslaMateChargeCosts().ConfigureAwait(false);
    }
}
