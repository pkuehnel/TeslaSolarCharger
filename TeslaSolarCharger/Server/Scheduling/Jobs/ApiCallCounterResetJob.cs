using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

public class ApiCallCounterResetJob(ILogger<ApiCallCounterResetJob> logger, ITeslaFleetApiService service) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        service.ResetApiRequestCounters();
    }
}
