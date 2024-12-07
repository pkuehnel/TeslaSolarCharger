using Quartz;
using Quartz.Spi;
using System.Collections.Concurrent;

namespace TeslaSolarCharger.Server.Scheduling;

public class JobFactory(ILogger<JobFactory> logger, IServiceProvider serviceProvider) : IJobFactory
{
    private readonly ConcurrentDictionary<IJob, IServiceScope> _scopes = new();

    public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        logger.LogTrace("{Method} ({@bundle}, {scheduler})", nameof(NewJob), bundle, scheduler);
        var jobType = bundle.JobDetail.JobType;
        var scope = serviceProvider.CreateScope();
        var job = (IJob)scope.ServiceProvider.GetRequiredService(jobType);
        _scopes.TryAdd(job, scope);
        return job;
    }

    public void ReturnJob(IJob job)
    {
        logger.LogTrace("{class}.{method}({job})", nameof(JobFactory), nameof(ReturnJob), job.GetType().FullName);
        if (_scopes.TryGetValue(job, out var scope))
        {
            scope.Dispose();
            _scopes.TryRemove(job, out _);
        }
    }
}
