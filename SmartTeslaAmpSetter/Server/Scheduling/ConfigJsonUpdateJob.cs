using Quartz;
using SmartTeslaAmpSetter.Server.Services;

namespace SmartTeslaAmpSetter.Server.Scheduling;

[DisallowConcurrentExecution]
public class ConfigJsonUpdateJob : IJob
{
    private readonly ILogger<ConfigJsonUpdateJob> _logger;
    private readonly ConfigJsonService _service;

    public ConfigJsonUpdateJob(ILogger<ConfigJsonUpdateJob> logger, ConfigJsonService service)
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