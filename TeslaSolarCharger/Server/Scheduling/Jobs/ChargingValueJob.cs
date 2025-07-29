using Quartz;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class ChargingValueJob(ILogger<ChargingValueJob> logger, IChargingService chargingService, IChargingServiceV2 chargingServiceV2) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        //var restPowerIncrease = await chargingService.SetNewChargingValues().ConfigureAwait(false);
        await chargingServiceV2.SetNewChargingValues(context.CancellationToken);
    }
}
