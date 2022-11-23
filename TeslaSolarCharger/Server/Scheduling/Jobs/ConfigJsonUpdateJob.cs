using Quartz;
using TeslaSolarCharger.Server.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class ConfigJsonUpdateJob : IJob
{
    private readonly ILogger<ConfigJsonUpdateJob> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public ConfigJsonUpdateJob(ILogger<ConfigJsonUpdateJob> logger, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }
    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogTrace("{method}({context})", nameof(Execute), context);
        using var scope = _serviceScopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IConfigJsonService>();
        await service.UpdateConfigJson().ConfigureAwait(false);
    }
}
