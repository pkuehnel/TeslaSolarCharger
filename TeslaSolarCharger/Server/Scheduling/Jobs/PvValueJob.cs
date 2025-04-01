using Quartz;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class PvValueJob(ILogger<PvValueJob> logger, IPvValueService service, IMeterValueLogService meterValueLogService) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await service.UpdatePvValues().ConfigureAwait(false);
        await meterValueLogService.LogPvValues().ConfigureAwait(false);
    }
}
