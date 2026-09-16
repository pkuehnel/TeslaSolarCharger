using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class HomeBatteryMinSocRefreshJob : IJob
{
    private readonly ILogger<HomeBatteryMinSocRefreshJob> _logger;
    private readonly IHomeBatteryEnergyCalculator _service;

    public HomeBatteryMinSocRefreshJob(ILogger<HomeBatteryMinSocRefreshJob> logger,
        IHomeBatteryEnergyCalculator service)
    {
        _logger = logger;
        _service = service;
    }
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({context})", nameof(Execute), context);
        await _service.RefreshHomeBatteryMinSoc(cancellationToken).ConfigureAwait(false);
    }
}
