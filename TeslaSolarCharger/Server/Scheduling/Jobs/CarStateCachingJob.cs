using Quartz;
using TeslaSolarCharger.Server.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class CarStateCachingJob : IJob
{
    private readonly ILogger<CarStateCachingJob> _logger;
    private readonly IConfigJsonService _service;

    public CarStateCachingJob(ILogger<CarStateCachingJob> logger, IConfigJsonService service)
    {
        _logger = logger;
        _service = service;
    }
    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogTrace("{method}({context})", nameof(Execute), context);
        await _service.CacheCarStates().ConfigureAwait(false);
    }
}
