using Quartz;
using TeslaSolarCharger.Server.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class NewVersionCheckJob : IJob
{
    private readonly ILogger<NewVersionCheckJob> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public NewVersionCheckJob(ILogger<NewVersionCheckJob> logger, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }
    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogTrace("{method}({context})", nameof(Execute), context);
        using var scope = _serviceScopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<INewVersionCheckService>();
        await service.CheckForNewVersion().ConfigureAwait(false);
    }
}
