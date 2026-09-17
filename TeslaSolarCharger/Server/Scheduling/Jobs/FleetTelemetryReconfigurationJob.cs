using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

public class FleetTelemetryReconfigurationJob(
    ILogger<FleetTelemetryReconfigurationJob> logger,
    IFleetTelemetryConfigurationService service) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await service.ReconfigureAllCarsIfRequired();
    }
}
