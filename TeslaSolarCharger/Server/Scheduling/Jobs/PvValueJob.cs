using Quartz;
using TeslaSolarCharger.Server.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class PvValueJob : IJob
{
    private readonly ILogger<PvValueJob> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public PvValueJob(ILogger<PvValueJob> logger, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }
    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogTrace("{method}({context})", nameof(Execute), context);
        using var scope = _serviceScopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPvValueService>();
        await service.UpdatePvValues().ConfigureAwait(false);
    }
}
