using Quartz;
using Quartz.Spi;

namespace SmartTeslaAmpSetter.Scheduling
{
    public class JobFactory : IJobFactory
    {
        private readonly ILogger<JobFactory> _logger;
        private readonly Job _job;

        public JobFactory(ILogger<JobFactory> logger, Job job)
        {
            _logger = logger;
            _job = job;
        }

        public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
        {
            _logger.LogTrace("New job requested");
            return _job;
        }

        public void ReturnJob(IJob job)
        {
            _logger.LogTrace("{class}.{method}({job})", nameof(JobFactory), nameof(ReturnJob), job.GetType().FullName);
        }
    }
}