using Quartz;
using TeslaSolarCharger.Server.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class PowerDistributionAddJob : IJob
{
    private readonly ILogger<ChargeTimeUpdateJob> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public PowerDistributionAddJob(ILogger<ChargeTimeUpdateJob> logger, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }
    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogTrace("{method}({context})", nameof(Execute), context);
        using var scope = _serviceScopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IChargingCostService>();
        await service.AddPowerDistributionForAllChargingCars().ConfigureAwait(false);
    }
}
