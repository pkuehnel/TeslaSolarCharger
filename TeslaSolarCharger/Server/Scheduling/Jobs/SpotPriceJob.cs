using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class SpotPriceJob(ILogger<SpotPriceJob> logger, ISpotPriceService service) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await service.UpdateSpotPrices().ConfigureAwait(false);
    }
}
