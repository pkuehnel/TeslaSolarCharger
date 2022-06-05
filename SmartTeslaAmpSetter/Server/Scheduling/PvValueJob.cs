using Quartz;
using SmartTeslaAmpSetter.Server.Contracts;

namespace SmartTeslaAmpSetter.Server.Scheduling;

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
        _logger.LogTrace("Executing Job to get PV values");
        await _service.UpdatePvValues().ConfigureAwait(false);
    }
}