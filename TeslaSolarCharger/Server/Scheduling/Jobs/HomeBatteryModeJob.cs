using Quartz;
using TeslaSolarCharger.Server.Services.HomeBatteryControl.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class HomeBatteryModeJob(ILogger<HomeBatteryModeJob> logger, IHomeBatteryModeService homeBatteryModeService) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await homeBatteryModeService.ApplyRequiredModeAsync(cancellationToken).ConfigureAwait(false);
    }
}
