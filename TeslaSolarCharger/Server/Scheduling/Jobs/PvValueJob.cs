using Quartz;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class PvValueJob(ILogger<PvValueJob> logger, IPvValueService service, IMeterValueLogService meterValueLogService) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await service.UpdatePvValues().ConfigureAwait(false);
        await meterValueLogService.AddPvValuesToBuffer().ConfigureAwait(false);
    }
}
