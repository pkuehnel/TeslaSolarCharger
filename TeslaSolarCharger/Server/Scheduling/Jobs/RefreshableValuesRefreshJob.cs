using Quartz;
using TeslaSolarCharger.Server.Services.SolarValueGathering.ValueRefresh.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

public class RefreshableValuesRefreshJob : IJob
{
    private readonly ILogger<RefreshableValuesRefreshJob> _logger;
    private readonly IRefreshableValueHandlingService _service;

    public RefreshableValuesRefreshJob(ILogger<RefreshableValuesRefreshJob> logger, IRefreshableValueHandlingService service)
    {
        _logger = logger;
        _service = service;
    }
    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogTrace("{method}({context})", nameof(Execute), context);
        await _service.RefreshValues().ConfigureAwait(false);
    }
}
