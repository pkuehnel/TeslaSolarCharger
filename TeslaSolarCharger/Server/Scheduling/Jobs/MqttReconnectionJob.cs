using Quartz;
using TeslaSolarCharger.Server.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class MqttReconnectionJob : IJob
{
    private readonly ILogger<MqttReconnectionJob> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public MqttReconnectionJob(ILogger<MqttReconnectionJob> logger, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }
    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogTrace("{method}({context})", nameof(Execute), context);
        using var scope = _serviceScopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMqttConnectionService>();
        await service.ReconnectMqttServices().ConfigureAwait(false);
    }
}
