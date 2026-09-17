using Quartz;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class ErrorMessagingJob(ILogger<ErrorMessagingJob> logger, IErrorHandlingService service) : IJob
{

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await service.SendTelegramMessages().ConfigureAwait(false);
    }
}
