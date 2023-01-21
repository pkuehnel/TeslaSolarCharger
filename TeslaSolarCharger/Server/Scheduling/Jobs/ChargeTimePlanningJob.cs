using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class ChargeTimePlanningJob : IJob
{
    private readonly ILogger<ChargeTimePlanningJob> _logger;
    private readonly IChargeTimePlanningService _service;

    public ChargeTimePlanningJob(ILogger<ChargeTimePlanningJob> logger, IChargeTimePlanningService service)
    {
        _logger = logger;
        _service = service;
    }
    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogTrace("{method}({context})", nameof(Execute), context);
        await _service.PlanChargeTimesForAllCars().ConfigureAwait(false);
    }
}
