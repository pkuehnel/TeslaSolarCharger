using TeslaSolarCharger.Server.Services.Contracts;
namespace TeslaSolarCharger.Server.Services;

public class DatabaseValueBufferFlushService : IHostedService
{
    private readonly ILogger<DatabaseValueBufferFlushService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public DatabaseValueBufferFlushService(ILogger<DatabaseValueBufferFlushService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}()", nameof(StopAsync));
        _logger.LogInformation("Application is stopping. Flushing buffered meter values to the database.");
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var meterValueLogService = scope.ServiceProvider.GetRequiredService<IMeterValueLogService>();
            await meterValueLogService.SaveBufferedMeterValuesToDatabase().ConfigureAwait(false);
            var chargerValueLogService = scope.ServiceProvider.GetRequiredService<IChargerValueLogService>();
            await chargerValueLogService.SaveBufferedChargerValuesToDatabase().ConfigureAwait(false);
            _logger.LogInformation("Flushed buffered meter values to the database.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while flushing buffered meter values to the database during shutdown.");
        }
    }
}
