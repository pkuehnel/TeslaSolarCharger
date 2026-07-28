using Quartz;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

public class BlePostCommandRefreshJob(
    ILogger<BlePostCommandRefreshJob> logger,
    IBleVehicleDataService bleVehicleDataService) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var carId = context.MergedJobDataMap.GetInt(BlePostCommandRefreshScheduler.CarIdJobDataKey);
        logger.LogTrace("{method}({carId})", nameof(Execute), carId);
        await bleVehicleDataService.RefreshSingleCarData(carId).ConfigureAwait(false);
    }
}
