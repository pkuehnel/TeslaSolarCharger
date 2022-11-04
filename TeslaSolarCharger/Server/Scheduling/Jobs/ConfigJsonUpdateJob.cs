using Quartz;
using TeslaSolarCharger.Server.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class ConfigJsonUpdateJob : IJob
{
    private readonly ILogger<ConfigJsonUpdateJob> _logger;
    private readonly IConfigJsonService _service;

    public ConfigJsonUpdateJob(ILogger<ConfigJsonUpdateJob> logger, IConfigJsonService service)
    {
        _logger = logger;
        _service = service;
    }
    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogTrace("{method}({context})", nameof(Execute), context);
        await _service.UpdateConfigJson().ConfigureAwait(false);
    }
}
