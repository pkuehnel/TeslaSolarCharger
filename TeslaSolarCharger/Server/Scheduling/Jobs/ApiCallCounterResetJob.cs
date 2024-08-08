using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

public class ApiCallCounterResetJob(ILogger<ApiCallCounterResetJob> logger,
    ITeslaFleetApiService service,
    IBackendApiService backendApiService,
    IDateTimeProvider dateTimeProvider,
    ISettings settings) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        if (settings.StartupTime < dateTimeProvider.UtcNow().AddMinutes(-10))
        {
            await backendApiService.PostTeslaApiCallStatistics().ConfigureAwait(false);
        }
        service.ResetApiRequestCounters();
        await backendApiService.GetNewBackendNotifications().ConfigureAwait(false);
    }
}
