using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class DatabaseBufferedValuesSaveJob(ILogger<DatabaseBufferedValuesSaveJob> logger,
    IMeterValueLogService meterValueLogService,
    IChargerValueLogService chargerValueLogService) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        try
        {
            await meterValueLogService.SaveBufferedMeterValuesToDatabase().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while saving buffered meter values to the database.");
        }
        await chargerValueLogService.SaveBufferedChargerValuesToDatabase().ConfigureAwait(false);
    }
}
