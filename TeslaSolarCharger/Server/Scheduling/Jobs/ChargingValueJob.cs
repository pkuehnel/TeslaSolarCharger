using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class ChargingValueJob(ILogger<ChargingValueJob> logger,
    IChargingServiceV2 chargingServiceV2,
    IBleVehicleDataService bleVehicleDataService) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        //Refresh data of BLE data collection cars right before new charging values are calculated so decisions are
        //based on up to date values. Errors are handled inside the service so charging value updates are never blocked.
        await bleVehicleDataService.RefreshBleCarData().ConfigureAwait(false);
        //var restPowerIncrease = await chargingService.SetNewChargingValues().ConfigureAwait(false);
        await chargingServiceV2.SetNewChargingValues(context.CancellationToken);
    }
}
