using Quartz;
using TeslaSolarCharger.Server.Contracts;

namespace TeslaSolarCharger.Server.Scheduling;

[DisallowConcurrentExecution]
public class CarDbUpdateJob : IJob
{
    private readonly ILogger<CarDbUpdateJob> _logger;
    private readonly ICarDbUpdateService _carDbUpdateService;

    public CarDbUpdateJob(ILogger<CarDbUpdateJob> logger, ICarDbUpdateService carDbUpdateService)
    {
        _logger = logger;
        _carDbUpdateService = carDbUpdateService;
    }
    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogTrace("Executing job to update cars from database");
        await _carDbUpdateService.UpdateCarsFromDatabase().ConfigureAwait(false);
    }
}