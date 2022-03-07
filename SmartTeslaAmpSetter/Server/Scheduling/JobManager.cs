using Quartz;
using Quartz.Spi;

namespace SmartTeslaAmpSetter.Server.Scheduling;

public class JobManager
{
    private readonly ILogger<JobManager> _logger;
    private readonly IJobFactory _jobFactory;
    private readonly ISchedulerFactory _schedulerFactory;

    private IScheduler _scheduler;


#pragma warning disable CS8618
    public JobManager(ILogger<JobManager> logger, IJobFactory jobFactory, ISchedulerFactory schedulerFactory)
#pragma warning restore CS8618
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

        var minimumJobIntervall = TimeSpan.FromSeconds(30);

        if (jobIntervall < minimumJobIntervall)
        {
            _logger.LogWarning("Jobintervall below {minimumJobIntervall}. Due to delays in TeslaMate and TeslaMate API use {minimum} as job interval", minimumJobIntervall, minimumJobIntervall);
            jobIntervall = minimumJobIntervall;
        }

        var chargeLogJob = JobBuilder.Create<Job>().Build();
        var configJsonUpdateJob = JobBuilder.Create<ConfigJsonUpdateJob>().Build();

        var defaultTrigger =
            TriggerBuilder.Create().WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever((int)jobIntervall.TotalSeconds)).Build();

        var updateJsonTrigger = TriggerBuilder.Create()
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever(10)).Build();

        var triggersAndJobs = new Dictionary<IJobDetail, IReadOnlyCollection<ITrigger>>
        {
            {chargeLogJob,  new HashSet<ITrigger> { defaultTrigger }},
            {configJsonUpdateJob, new HashSet<ITrigger> {updateJsonTrigger}},
        };

        await _scheduler.ScheduleJobs(triggersAndJobs, false).ConfigureAwait(false);

        await _scheduler.Start().ConfigureAwait(false);
    }
}