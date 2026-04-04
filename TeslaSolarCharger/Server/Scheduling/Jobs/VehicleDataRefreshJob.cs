using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class VehicleDataRefreshJob(ILogger<VehicleDataRefreshJob> logger, ICarDataProviderOrchestrator orchestrator) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await orchestrator.RefreshAllCarData().ConfigureAwait(false);
    }
}
