using Quartz;
using TeslaSolarCharger.Server.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class ChargingValueJob : IJob
{
    private readonly ILogger<ChargingValueJob> _logger;
    private readonly IChargingService _chargingService;

    public ChargingValueJob(ILogger<ChargingValueJob> logger, IChargingService chargingService)
    {
        _logger = logger;
        _chargingService = chargingService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogTrace("Executing Job to set ChargerValues");
        await _chargingService.SetNewChargingValues().ConfigureAwait(false);
    }
}