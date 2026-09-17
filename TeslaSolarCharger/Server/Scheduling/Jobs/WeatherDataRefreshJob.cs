using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class WeatherDataRefreshJob(ILogger<WeatherDataRefreshJob> logger, IWeatherDataService service, IEnergyDataService energyDataService) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        try
        {
            await service.RefreshWeatherData().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while refreshing weather data");
        }
        await energyDataService.RefreshCachedValues(cancellationToken).ConfigureAwait(false);
    }
}
