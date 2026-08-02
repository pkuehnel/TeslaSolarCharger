using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

/// <summary>
/// Refreshes the data of all BLE data collection cars. Deliberately decoupled from the charging cycle: a car that is
/// slow to answer or absent used to delay the charging value calculation by the time its BLE reads took.
/// </summary>
[DisallowConcurrentExecution]
public class BleDataRefreshJob(ILogger<BleDataRefreshJob> logger,
    IBleVehicleDataService bleVehicleDataService) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        //Errors are handled inside the service, so a failing car never stops the others.
        await bleVehicleDataService.RefreshBleCarData().ConfigureAwait(false);
    }
}
