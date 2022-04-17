using Quartz;
using SmartTeslaAmpSetter.Server.Contracts;

namespace SmartTeslaAmpSetter.Server.Scheduling;

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
        _logger.LogTrace("Executing Job to update Config.json");
        await _service.UpdateConfigJson().ConfigureAwait(false);
    }
}