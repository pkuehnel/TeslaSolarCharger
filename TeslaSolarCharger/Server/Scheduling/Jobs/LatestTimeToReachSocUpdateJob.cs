using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

public class LatestTimeToReachSocUpdateJob : IJob
{
    private readonly ILogger<LatestTimeToReachSocUpdateJob> _logger;
    private readonly ILatestTimeToReachSocUpdateService _service;

    public LatestTimeToReachSocUpdateJob(ILogger<LatestTimeToReachSocUpdateJob> logger, ILatestTimeToReachSocUpdateService service)
    {
        _logger = logger;
        _service = service;
    }
    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogTrace("{method}({context})", nameof(Execute), context);
        await Task.Run(() => _service.UpdateAllCars()).ConfigureAwait(false);
    }
}
