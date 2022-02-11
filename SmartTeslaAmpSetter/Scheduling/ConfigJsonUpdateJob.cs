using Quartz;
using SmartTeslaAmpSetter.Services;

namespace SmartTeslaAmpSetter.Scheduling;

[DisallowConcurrentExecution]
public class ConfigJsonUpdateJob : IJob
{
    private readonly ILogger<ConfigJsonUpdateJob> _logger;
    private readonly ConfigJsonUpdateService _service;

    public ConfigJsonUpdateJob(ILogger<ConfigJsonUpdateJob> logger, ConfigJsonUpdateService service)
    {
        _logger = logger;
        _service = service;
    }
    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogTrace("Executing Job to update Config.json");
        await _service.UpdateConfigJson().ConfigureAwait(false);
    }
}