using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class ChargingValueJob(ILogger<ChargingValueJob> logger,
    IChargingServiceV2 chargingServiceV2) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        //BLE data of BLE data collection cars is refreshed by BleDataRefreshJob on its own schedule, so a car that is
        //slow to answer or absent can not delay the charging value calculation anymore.
        //var restPowerIncrease = await chargingService.SetNewChargingValues().ConfigureAwait(false);
        await chargingServiceV2.SetNewChargingValues(context.CancellationToken);
    }
}
