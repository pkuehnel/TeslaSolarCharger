using TeslaSolarCharger.Server.Services.ApiServices.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
namespace TeslaSolarCharger.Server.Services;

public class ValueFlushService(ILogger<ValueFlushService> logger, IServiceProvider serviceProvider) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}()", nameof(StopAsync));
        logger.LogInformation("Application is stopping. Flushing buffered values to the database.");
        try
        {
            using var scope = serviceProvider.CreateScope();
            logger.LogInformation("Flushing buffered meter values to the database.");
            var meterValueLogService = scope.ServiceProvider.GetRequiredService<IMeterValueLogService>();
            await meterValueLogService.SaveBufferedMeterValuesToDatabase().ConfigureAwait(false);
            logger.LogInformation("Flushed buffered meter values to the database.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while flushing buffered meter values to the database during shutdown.");
        }
        try
        {
            using var scope = serviceProvider.CreateScope();
            logger.LogInformation("Flushing buffered charging detail values to the database.");
            var chargingCostService = scope.ServiceProvider.GetRequiredService<ITscOnlyChargingCostService>();
            await chargingCostService.AddChargingDataToDatabase().ConfigureAwait(false);
            logger.LogInformation("Flushed buffered charging detail values to the database.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while flushing buffered meter values to the database during shutdown.");
        }
    }
}
