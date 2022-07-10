using Quartz;
using Quartz.Spi;

namespace SolarTeslaCharger.Server.Scheduling;

public class JobFactory : IJobFactory
{
    private readonly ILogger<JobFactory> _logger;
    private readonly IServiceProvider _serviceProvider;

    public JobFactory(ILogger<JobFactory> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        _logger.LogTrace("{Method} ({@bundle}, {scheduler})", nameof(NewJob), bundle, scheduler);
        return (IJob)_serviceProvider.GetRequiredService(bundle.JobDetail.JobType);
    }

    public void ReturnJob(IJob job)
    {
        _logger.LogTrace("{class}.{method}({job})", nameof(JobFactory), nameof(ReturnJob), job.GetType().FullName);
    }
}