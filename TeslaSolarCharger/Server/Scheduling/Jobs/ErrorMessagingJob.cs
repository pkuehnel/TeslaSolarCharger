using Quartz;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class ErrorMessagingJob(ILogger<ErrorMessagingJob> logger, IErrorHandlingService service) : IJob
{

    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await service.SendTelegramMessages().ConfigureAwait(false);
    }
}
