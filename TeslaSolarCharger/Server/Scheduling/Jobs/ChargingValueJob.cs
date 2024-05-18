using Quartz;
using TeslaSolarCharger.Server.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class ChargingValueJob(ILogger<ChargingValueJob> logger, IChargingService chargingService) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await chargingService.SetNewChargingValues().ConfigureAwait(false);
    }
}
