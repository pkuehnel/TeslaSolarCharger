using TeslaSolarCharger.Server.Services.Contracts;
namespace TeslaSolarCharger.Server.Services;

public class MeterValueFlushService(ILogger<MeterValueFlushService> logger, IMeterValueLogService meterValueLogService) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}()", nameof(StopAsync));
        logger.LogInformation("Application is stopping. Flushing buffered meter values to the database.");
        await meterValueLogService.SaveBufferedMeterValuesToDatabase().ConfigureAwait(false);
        logger.LogInformation("Flushed buffered meter values to the database.");
    }
}
