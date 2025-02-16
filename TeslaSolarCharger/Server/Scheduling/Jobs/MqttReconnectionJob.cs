using Quartz;
using TeslaSolarCharger.Server.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class MqttReconnectionJob(ILogger<MqttReconnectionJob> logger, IMqttConnectionService service) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await service.ReconnectMqttServices().ConfigureAwait(false);
    }
}
