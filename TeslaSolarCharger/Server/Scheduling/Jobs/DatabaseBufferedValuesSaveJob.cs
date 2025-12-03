using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class DatabaseBufferedValuesSaveJob(ILogger<DatabaseBufferedValuesSaveJob> logger,
    IMeterValueLogService meterValueLogService,
    IChargerValueLogService chargerValueLogService,
    ICarValueEstimationService carValueEstimationService) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}()", nameof(Execute));
        await ExecuteWithoutContext(context.CancellationToken);
    }

    public async Task ExecuteWithoutContext(CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}()", nameof(ExecuteWithoutContext));
        try
        {
            await meterValueLogService.SaveBufferedMeterValuesToDatabase().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while saving buffered meter values to the database.");
        }

        try
        {
            await chargerValueLogService.SaveBufferedChargerValuesToDatabase().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while saving buffered charger meter values to the database.");
        }

        await carValueEstimationService.UpdateAllCarValueEstimations(cancellationToken).ConfigureAwait(false);
    }
}
