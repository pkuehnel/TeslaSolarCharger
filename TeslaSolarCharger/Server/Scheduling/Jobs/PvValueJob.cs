using Quartz;
using TeslaSolarCharger.Server.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class PvValueJob : IJob
{
    private readonly ILogger<PvValueJob> _logger;
    private readonly IPvValueService _service;

    public PvValueJob(ILogger<PvValueJob> logger, IPvValueService service)
    {
        _logger = logger;
        _service = service;
    }
    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogTrace("{method}({context})", nameof(Execute), context);
        await _service.UpdatePvValues().ConfigureAwait(false);
    }
}
