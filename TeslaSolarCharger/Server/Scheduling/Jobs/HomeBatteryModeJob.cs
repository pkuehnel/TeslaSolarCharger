using Quartz;
using TeslaSolarCharger.Server.Services.HomeBatteryControl.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class HomeBatteryModeJob(ILogger<HomeBatteryModeJob> logger, IHomeBatteryModeService homeBatteryModeService) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await homeBatteryModeService.ApplyRequiredModeAsync(context.CancellationToken).ConfigureAwait(false);
    }
}
