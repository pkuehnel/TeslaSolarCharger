using Quartz;
using TeslaSolarCharger.Server.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class NewVersionCheckJob(ILogger<NewVersionCheckJob> logger, INewVersionCheckService service) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await service.CheckForNewVersion().ConfigureAwait(false);
    }
}
