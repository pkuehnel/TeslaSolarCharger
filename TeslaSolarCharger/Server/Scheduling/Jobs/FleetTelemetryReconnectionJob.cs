using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

public class FleetTelemetryReconnectionJob(
    ILogger<FleetTelemetryReconnectionJob> logger,
    IFleetTelemetryWebSocketService service) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await service.ReconnectWebSocketsForEnabledCars();
    }
}
