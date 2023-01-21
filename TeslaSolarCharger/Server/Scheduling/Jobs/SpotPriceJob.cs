using Quartz;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

public class SpotPriceJob : IJob
{
    private readonly ILogger<SpotPriceJob> _logger;
    private readonly ISpotPriceService _service;

    public SpotPriceJob(ILogger<SpotPriceJob> logger, ISpotPriceService service)
    {
        _logger = logger;
        _service = service;
    }


    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogTrace("{method}({context})", nameof(Execute), context);
        await Task.Run(() => _service.UpdateSpotPrices()).ConfigureAwait(false);
    }
}
