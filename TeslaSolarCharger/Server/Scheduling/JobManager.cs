using Quartz;
using Quartz.Spi;
using TeslaSolarCharger.Shared.Contracts;

namespace TeslaSolarCharger.Server.Scheduling;

public class JobManager
{
    private readonly ILogger<JobManager> _logger;
    private readonly IJobFactory _jobFactory;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IConfigurationWrapper _configurationWrapper;

    private IScheduler _scheduler;


#pragma warning disable CS8618
    public JobManager(ILogger<JobManager> logger, IJobFactory jobFactory, ISchedulerFactory schedulerFactory, IConfigurationWrapper configurationWrapper)
#pragma warning restore CS8618
    {
        _logger = logger;
        _jobFactory = jobFactory;
        _schedulerFactory = schedulerFactory;
        _configurationWrapper = configurationWrapper;
    }

    public async void StartJobs()
    {
        _logger.LogTrace("{Method}()", nameof(StartJobs));
        _scheduler = _schedulerFactory.GetScheduler().GetAwaiter().GetResult();
        _scheduler.JobFactory = _jobFactory;

        var chargingValueJob = JobBuilder.Create<ChargingValueJob>().Build();
        var configJsonUpdateJob = JobBuilder.Create<ConfigJsonUpdateJob>().Build();
        var chargeTimeUpdateJob = JobBuilder.Create<ChargeTimeUpdateJob>().Build();
        var pvValueJob = JobBuilder.Create<PvValueJob>().Build();
        var carDbUpdateJob = JobBuilder.Create<CarDbUpdateJob>().Build();

        var jobIntervall = _configurationWrapper.ChargingValueJobUpdateIntervall();

        var chargingValueTrigger =
            TriggerBuilder.Create().WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever((int)jobIntervall.TotalSeconds)).Build();

        var updateJsonTrigger = TriggerBuilder.Create()
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever(10)).Build();

        var chargeTimeUpdateTrigger = TriggerBuilder.Create()
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever(30)).Build();

        var pvValueJobIntervall = _configurationWrapper.PvValueJobUpdateIntervall();
        _logger.LogTrace("PvValue Job intervall is {pvValueJobIntervall}", pvValueJobIntervall);

        var pvValueTrigger = TriggerBuilder.Create()
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever((int)pvValueJobIntervall.TotalSeconds)).Build();

        var triggersAndJobs = new Dictionary<IJobDetail, IReadOnlyCollection<ITrigger>>
        {
            {chargingValueJob,  new HashSet<ITrigger> { chargingValueTrigger }},
            {configJsonUpdateJob, new HashSet<ITrigger> {updateJsonTrigger}},
            {chargeTimeUpdateJob, new HashSet<ITrigger> {chargeTimeUpdateTrigger}},
            {pvValueJob, new HashSet<ITrigger> {pvValueTrigger}},
        };

        await _scheduler.ScheduleJobs(triggersAndJobs, false).ConfigureAwait(false);

        await _scheduler.Start().ConfigureAwait(false);
    }
}