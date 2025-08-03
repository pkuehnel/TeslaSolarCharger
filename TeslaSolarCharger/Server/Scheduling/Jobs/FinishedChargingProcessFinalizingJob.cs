using Quartz;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class FinishedChargingProcessFinalizingJob(
    ILogger<FinishedChargingProcessFinalizingJob> logger,
    ITscOnlyChargingCostService service)
    : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await service.AddChargingDataToDatabase().ConfigureAwait(false);
    }
}
