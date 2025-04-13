using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class MeterValueDatabaseSaveJob(ILogger<MeterValueDatabaseSaveJob> logger, IMeterValueLogService service) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await service.SaveBufferdMeterValuesToDatabase().ConfigureAwait(false);
    }
}
