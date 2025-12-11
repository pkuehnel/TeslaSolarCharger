using TeslaSolarCharger.Server.Scheduling.Jobs;
namespace TeslaSolarCharger.Server.Services;

public class DatabaseValueBufferFlushService : IHostedService
{
    private readonly ILogger<DatabaseValueBufferFlushService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public DatabaseValueBufferFlushService(ILogger<DatabaseValueBufferFlushService> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
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
            using var scope = _serviceScopeFactory.CreateScope();
            var databaseBufferedValuesSaveJob = scope.ServiceProvider.GetRequiredService<DatabaseBufferedValuesSaveJob>();
            await databaseBufferedValuesSaveJob.ExecuteWithoutContext(cancellationToken);
            _logger.LogInformation("Flushed buffered values to the database.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while flushing buffered values to the database during shutdown.");
        }
    }
}
