using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

public class BleApiVersionDetectionJob(
    ILogger<BleApiVersionDetectionJob> logger,
    IBleService service) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await service.CheckBleApiVersionCompatibilities();
    }
}
