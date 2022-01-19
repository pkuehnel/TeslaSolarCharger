using Quartz;
using Quartz.Spi;

namespace SmartTeslaAmpSetter.Scheduling
{
    public class JobManager
    {
        private readonly ILogger<JobManager> _logger;
        private readonly IJobFactory _jobFactory;
        private readonly ISchedulerFactory _schedulerFactory;

        private IScheduler _scheduler;


        public JobManager(ILogger<JobManager> logger, IJobFactory jobFactory, ISchedulerFactory schedulerFactory)
        {
            _logger = logger;
            _jobFactory = jobFactory;
            _schedulerFactory = schedulerFactory;
        }

        public async void StartJobs(TimeSpan jobIntervall)
        {
            _logger.LogTrace("{class}.{Method}()", nameof(JobManager), nameof(StartJobs));
            _scheduler = _schedulerFactory.GetScheduler().GetAwaiter().GetResult();
            _scheduler.JobFactory = _jobFactory;

            var chargeLogJob = JobBuilder.Create<Job>().Build();

            var defaultTrigger =
                TriggerBuilder.Create().WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever((int)jobIntervall.TotalSeconds)).Build();

            var triggersAndJobs = new Dictionary<IJobDetail, IReadOnlyCollection<ITrigger>>
            {
                {chargeLogJob,  new HashSet<ITrigger> { defaultTrigger }},
            };

            await _scheduler.ScheduleJobs(triggersAndJobs, false).ConfigureAwait(false);

            await _scheduler.Start().ConfigureAwait(false);
        }
    }
}