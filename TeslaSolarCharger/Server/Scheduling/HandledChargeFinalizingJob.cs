using Quartz;
using TeslaSolarCharger.Server.Contracts;

namespace TeslaSolarCharger.Server.Scheduling;

public class HandledChargeFinalizingJob : IJob
{
    private readonly ILogger<ChargeTimeUpdateJob> _logger;
    private readonly IChargingCostService _service;

    public HandledChargeFinalizingJob(ILogger<ChargeTimeUpdateJob> logger, IChargingCostService service)
    {
        _logger = logger;
        _service = service;
    }
    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogTrace("Executing Job to update ChargeTimes");
        await _service.FinalizeHandledCharges().ConfigureAwait(false);
    }
}
