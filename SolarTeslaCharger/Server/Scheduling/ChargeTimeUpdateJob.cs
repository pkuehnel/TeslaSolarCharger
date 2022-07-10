using Quartz;
using SolarTeslaCharger.Server.Contracts;

namespace SolarTeslaCharger.Server.Scheduling;

[DisallowConcurrentExecution]
public class ChargeTimeUpdateJob : IJob
{
    private readonly ILogger<ChargeTimeUpdateJob> _logger;
    private readonly IChargeTimeUpdateService _service;

    public ChargeTimeUpdateJob(ILogger<ChargeTimeUpdateJob> logger, IChargeTimeUpdateService service)
    {
        _logger = logger;
        _service = service;
    }
    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogTrace("Executing Job to update ChargeTimes");
        await Task.Run(() => _service.UpdateChargeTimes());
    }
}