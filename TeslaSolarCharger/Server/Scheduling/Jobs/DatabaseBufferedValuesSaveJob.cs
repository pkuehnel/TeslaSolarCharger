using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Server.SignalR.Notifiers.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.SignalRClients;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class DatabaseBufferedValuesSaveJob(ILogger<DatabaseBufferedValuesSaveJob> logger,
    IMeterValueLogService meterValueLogService,
    IChargerValueLogService chargerValueLogService,
    ICarValueEstimationService carValueEstimationService,
    IAppStateNotifier appStateNotifier,
    IDateTimeProvider dateTimeProvider) : IJob
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

        var energyPredictionChange = new StateUpdateDto
        {
            DataType = DataTypeConstants.EnergyPredictionChangeTrigger,
            Timestamp = dateTimeProvider.DateTimeOffSetUtcNow(),
        };
        await appStateNotifier.NotifyStateUpdateAsync(energyPredictionChange).ConfigureAwait(false);
    }
}
