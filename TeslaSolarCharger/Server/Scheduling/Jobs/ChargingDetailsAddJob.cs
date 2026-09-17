using Quartz;
using TeslaSolarCharger.Server.Services.ApiServices.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class ChargingDetailsAddJob(ILogger<ChargingDetailsAddJob> logger,
    ITscOnlyChargingCostService service)
    : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await service.AddChargingDetailsForAllCars().ConfigureAwait(false);
    }
}
