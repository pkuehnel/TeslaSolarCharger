using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class FleetApiTokenRefreshJob(ILogger<FleetApiTokenRefreshJob> logger,
    ITeslaFleetApiService service)
    : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await service.GetNewTokenFromBackend().ConfigureAwait(false);
        await service.RefreshTokensIfAllowedAndNeeded().ConfigureAwait(false);
    }
}
