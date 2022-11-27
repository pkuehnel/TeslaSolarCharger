using Quartz;
using TeslaSolarCharger.Server.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class NewVersionCheckJob : IJob
{
    private readonly ILogger<NewVersionCheckJob> _logger;
    private readonly INewVersionCheckService _service;

    public NewVersionCheckJob(ILogger<NewVersionCheckJob> logger, INewVersionCheckService service)
    {
        _logger = logger;
        _service = service;
    }
    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogTrace("{method}({context})", nameof(Execute), context);
        await _service.CheckForNewVersion().ConfigureAwait(false);
    }
}
