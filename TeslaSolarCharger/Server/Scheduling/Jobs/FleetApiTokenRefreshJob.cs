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
        await service.RefreshFleetApiRequestsAreAllowed().ConfigureAwait(false);
        var newTokenReceived = await service.GetNewTokenFromBackend().ConfigureAwait(false);
        if (newTokenReceived)
        {
            logger.LogInformation("A new Tesla Token was received.");

        }
        await service.RefreshTokensIfAllowedAndNeeded().ConfigureAwait(false);
    }
}
