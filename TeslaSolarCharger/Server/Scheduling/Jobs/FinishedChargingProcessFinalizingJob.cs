using Quartz;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

public class FinishedChargingProcessFinalizingJob(
    ILogger<FinishedChargingProcessFinalizingJob> logger,
    ITscOnlyChargingCostService service)
    : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await service.FinalizeFinishedChargingProcesses().ConfigureAwait(false);
    }
}
