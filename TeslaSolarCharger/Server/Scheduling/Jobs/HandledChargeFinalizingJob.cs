using Quartz;
using TeslaSolarCharger.Server.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

public class HandledChargeFinalizingJob : IJob
{
    private readonly ILogger<HandledChargeFinalizingJob> _logger;
    private readonly IChargingCostService _service;

    public HandledChargeFinalizingJob(ILogger<HandledChargeFinalizingJob> logger, IChargingCostService service)
    {
        _logger = logger;
        _service = service;
    }
    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogTrace("{method}({context})", nameof(Execute), context);
        await _service.FinalizeHandledCharges().ConfigureAwait(false);
    }
}
