using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class ManualCarsDataClearingJob : IJob
{
    private readonly ILogger<ManualCarsDataClearingJob> _logger;
    private readonly ICarValueEstimationService _carValueEstimationService;

    public ManualCarsDataClearingJob(ILogger<ManualCarsDataClearingJob> logger, ICarValueEstimationService carValueEstimationService)
    {
        _logger = logger;
        _carValueEstimationService = carValueEstimationService;
    }
    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogTrace("{method}()", nameof(Execute));
        await _carValueEstimationService.PlugoutCarsAndClearSocIfRequired(context.CancellationToken);
    }
}
