using Quartz;
using TeslaSolarCharger.Server.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class PowerDistributionAddJob : IJob
{
    private readonly ILogger<ChargeTimeUpdateJob> _logger;
    private readonly IChargingCostService _service;

    public PowerDistributionAddJob(ILogger<ChargeTimeUpdateJob> logger, IChargingCostService service)
    {
        _logger = logger;
        _service = service;
    }
    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogTrace("Executing Job to update ChargeTimes");
        await _service.AddPowerDistributionForAllChargingCars().ConfigureAwait(false);
    }
}
