using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

public class ApiCallCounterResetJob(ILogger<ApiCallCounterResetJob> logger, ITeslaFleetApiService service, IBackendApiService backendApiService) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await backendApiService.PostTeslaApiCallStatistics().ConfigureAwait(false);
        service.ResetApiRequestCounters();
        await backendApiService.GetNewBackendNotifications().ConfigureAwait(false);
    }
}
