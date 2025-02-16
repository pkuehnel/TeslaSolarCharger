using Quartz;
using TeslaSolarCharger.Server.Services;
using TeslaSolarCharger.Server.Services.Contracts;

namespace TeslaSolarCharger.Server.Scheduling.Jobs;

[DisallowConcurrentExecution]
public class ErrorDetectionJob(ILogger<ErrorDetectionJob> logger, IErrorDetectionService service) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogTrace("{method}({context})", nameof(Execute), context);
        await service.DetectErrors().ConfigureAwait(false);
    }
}
