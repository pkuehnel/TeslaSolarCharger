using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class BleApiVersionDetectionJob(
    ILogger<BleApiVersionDetectionJob> logger,
    IBleService service) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await service.CheckBleApiVersionCompatibilities();
    }
}
