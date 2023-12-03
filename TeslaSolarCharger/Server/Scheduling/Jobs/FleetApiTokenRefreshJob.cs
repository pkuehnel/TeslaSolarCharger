using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class FleetApiTokenRefreshJob : IJob
{
    private readonly ILogger<FleetApiTokenRefreshJob> _logger;
    private readonly ITeslaFleetApiService _service;

    public FleetApiTokenRefreshJob(ILogger<FleetApiTokenRefreshJob> logger, ITeslaFleetApiService service)
    {
        _logger = logger;
        _service = service;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogTrace("{method}({context})", nameof(Execute), context);
        await _service.RefreshTokenAsync().ConfigureAwait(false);
    }
}
