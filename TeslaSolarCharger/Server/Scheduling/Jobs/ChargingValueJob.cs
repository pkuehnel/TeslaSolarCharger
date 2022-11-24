using Quartz;
using TeslaSolarCharger.Server.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class ChargingValueJob : IJob
{
    private readonly ILogger<ChargingValueJob> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public ChargingValueJob(ILogger<ChargingValueJob> logger, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogTrace("{method}({context})", nameof(Execute), context);
        using var scope = _serviceScopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IChargingService>();
        await service.SetNewChargingValues().ConfigureAwait(false);
    }
}
