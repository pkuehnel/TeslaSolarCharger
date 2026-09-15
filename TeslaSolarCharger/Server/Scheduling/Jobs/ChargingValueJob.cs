using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class ChargingValueJob(ILogger<ChargingValueJob> logger,
    IChargingServiceV2 chargingServiceV2,
    IDeferredChargingTestObserver deferredChargingTestObserver) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        //BLE data of BLE data collection cars is refreshed by BleDataRefreshJob on its own schedule, so a car that is
        //slow to answer or absent can not delay the charging value calculation anymore.
        //var restPowerIncrease = await chargingService.SetNewChargingValues().ConfigureAwait(false);
        await chargingServiceV2.SetNewChargingValues(context.CancellationToken);

        //A charging test the user postponed during setup is settled by the car actually charging, which is exactly
        //what has just been decided here. Wrapped so a problem with a setup note can never stop charging.
        try
        {
            await deferredChargingTestObserver.ObserveRunningCharges(context.CancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not settle postponed charging tests.");
        }
    }
}
