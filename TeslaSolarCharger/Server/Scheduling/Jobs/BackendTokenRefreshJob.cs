using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class BackendTokenRefreshJob(ILogger<BackendTokenRefreshJob> logger,
    IBackendApiService service)
    : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await service.RefreshBackendToken().ConfigureAwait(false);
    }
}
