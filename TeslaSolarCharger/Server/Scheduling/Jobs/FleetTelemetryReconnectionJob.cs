using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

public class FleetTelemetryReconnectionJob(
    ILogger<BleApiVersionDetectionJob> logger,
    IFleetTelemetryWebSocketService service,
    IFleetTelemetryConfigurationService fleetTelemetryConfigurationService) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await fleetTelemetryConfigurationService.ReconfigureAllCarsIfRequired();
        await service.ReconnectWebSocketsForEnabledCars();
    }
}
