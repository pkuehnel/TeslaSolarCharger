using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

public class WeatherDataRefreshJob(ILogger<WeatherDataRefreshJob> logger, IWeatherDataService service) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await service.RefreshWeatherData().ConfigureAwait(false);
    }
}
