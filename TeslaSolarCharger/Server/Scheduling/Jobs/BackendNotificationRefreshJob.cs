using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

public class BackendNotificationRefreshJob(ILogger<BackendNotificationRefreshJob> logger,
    ITeslaFleetApiService service,
    IBackendApiService backendApiService) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        service.ResetApiRequestCounters();
        await backendApiService.GetNewBackendNotifications().ConfigureAwait(false);
    }
}
