using Quartz;
using TeslaSolarCharger.Server.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class MqttReconnectionJob : IJob
{
    private readonly ILogger<MqttReconnectionJob> _logger;
    private readonly IMqttConnectionService _service;

    public MqttReconnectionJob(ILogger<MqttReconnectionJob> logger, IMqttConnectionService service)
    {
        _logger = logger;
        _service = service;
    }
    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogTrace("{method}({context})", nameof(Execute), context);
        await _service.ReconnectMqttServices().ConfigureAwait(false);
    }
}
