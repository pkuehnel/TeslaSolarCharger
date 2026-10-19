using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class UsableEnergyLearningJob(
    ILogger<UsableEnergyLearningJob> logger,
    IUsableEnergyLearningService usableEnergyLearningService) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await usableEnergyLearningService.RefreshAsync(cancellationToken).ConfigureAwait(false);
    }
}
