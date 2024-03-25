using Quartz;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class ChargingDetailsAddJob(ILogger<ChargingDetailsAddJob> logger,
    ITscOnlyChargingCostService service)
    : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await service.AddChargingDetailsForAllCars().ConfigureAwait(false);
    }
}
