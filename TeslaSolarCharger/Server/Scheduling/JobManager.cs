using Quartz;
using Quartz.Spi;
using TeslaSolarCharger.Server.Scheduling.Jobs;
using TeslaSolarCharger.Shared.Contracts;

namespace TeslaSolarCharger.Server.Scheduling;

public class JobManager
{
    private readonly ILogger<JobManager> _logger;
    private readonly IJobFactory _jobFactory;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly IDateTimeProvider _dateTimeProvider;

    private IScheduler? _scheduler;


#pragma warning disable CS8618
    public JobManager(ILogger<JobManager> logger, IJobFactory jobFactory, ISchedulerFactory schedulerFactory,
        IConfigurationWrapper configurationWrapper, IDateTimeProvider dateTimeProvider)
#pragma warning restore CS8618
    {
        _logger = logger;
        _jobFactory = jobFactory;
        _schedulerFactory = schedulerFactory;
        _configurationWrapper = configurationWrapper;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task StartJobs()
    {
        _logger.LogTrace("{Method}()", nameof(StartJobs));
        _scheduler = _schedulerFactory.GetScheduler().GetAwaiter().GetResult();
        _scheduler.JobFactory = _jobFactory;

        var chargingValueJob = JobBuilder.Create<ChargingValueJob>().Build();
        var carStateCachingJob = JobBuilder.Create<CarStateCachingJob>().Build();
        var pvValueJob = JobBuilder.Create<PvValueJob>().Build();
        var powerDistributionAddJob = JobBuilder.Create<PowerDistributionAddJob>().Build();
        var handledChargeFinalizingJob = JobBuilder.Create<HandledChargeFinalizingJob>().Build();
        var mqttReconnectionJob = JobBuilder.Create<MqttReconnectionJob>().Build();
        var newVersionCheckJob = JobBuilder.Create<NewVersionCheckJob>().Build();
        var spotPriceJob = JobBuilder.Create<SpotPriceJob>().Build();
        var fleetApiTokenRefreshJob = JobBuilder.Create<FleetApiTokenRefreshJob>().Build();

        var currentDate = _dateTimeProvider.DateTimeOffSetNow();
        var chargingTriggerStartTime = currentDate.AddSeconds(5);
        var pvTriggerStartTime = currentDate.AddSeconds(3);

        var chargingValueJobUpdateIntervall = _configurationWrapper.ChargingValueJobUpdateIntervall();

        var chargingValueTrigger = TriggerBuilder.Create()
            .StartAt(chargingTriggerStartTime)
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever((int)chargingValueJobUpdateIntervall.TotalSeconds))
            .Build();


        var pvValueJobIntervall = _configurationWrapper.PvValueJobUpdateIntervall();
        _logger.LogTrace("PvValue Job intervall is {pvValueJobIntervall}", pvValueJobIntervall);

        var pvValueTrigger = TriggerBuilder.Create()
            .StartAt(pvTriggerStartTime)
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever((int)pvValueJobIntervall.TotalSeconds))
            .Build();

        var carStateCachingTrigger = TriggerBuilder.Create()
            .WithSchedule(SimpleScheduleBuilder.RepeatMinutelyForever(3)).Build();

        var powerDistributionAddTrigger = TriggerBuilder.Create()
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever(16)).Build();

        var handledChargeFinalizingTrigger = TriggerBuilder.Create()
            .WithSchedule(SimpleScheduleBuilder.RepeatMinutelyForever(9)).Build();

        var mqttReconnectionTrigger = TriggerBuilder.Create()
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever(54)).Build();

        var newVersionCheckTrigger = TriggerBuilder.Create()
            .WithSchedule(SimpleScheduleBuilder.RepeatHourlyForever(47)).Build();

        var spotPricePlanningTrigger = TriggerBuilder.Create()
            .WithSchedule(SimpleScheduleBuilder.RepeatHourlyForever(1)).Build();

        var fleetApiTokenRefreshTrigger = TriggerBuilder.Create()
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever(59)).Build();

        var triggersAndJobs = new Dictionary<IJobDetail, IReadOnlyCollection<ITrigger>>
        {
            {chargingValueJob,  new HashSet<ITrigger> { chargingValueTrigger }},
            {carStateCachingJob, new HashSet<ITrigger> {carStateCachingTrigger}},
            {pvValueJob, new HashSet<ITrigger> {pvValueTrigger}},
            {powerDistributionAddJob, new HashSet<ITrigger> {powerDistributionAddTrigger}},
            {handledChargeFinalizingJob, new HashSet<ITrigger> {handledChargeFinalizingTrigger}},
            {mqttReconnectionJob, new HashSet<ITrigger> {mqttReconnectionTrigger}},
            {newVersionCheckJob, new HashSet<ITrigger> {newVersionCheckTrigger}},
            {spotPriceJob, new HashSet<ITrigger> {spotPricePlanningTrigger}},
            {fleetApiTokenRefreshJob, new HashSet<ITrigger> {fleetApiTokenRefreshTrigger}},
        };

        await _scheduler.ScheduleJobs(triggersAndJobs, false).ConfigureAwait(false);

        await _scheduler.Start().ConfigureAwait(false);
    }

    public async Task<bool> StopJobs()
    {
        _logger.LogTrace("{method}()", nameof(StopJobs));
        if (_scheduler == null)
        {
            _logger.LogInformation("Jobs were not running, yet, so stop is not needed.");
            return false;
        }
        await _scheduler.Shutdown(true).ConfigureAwait(false);
        return true;
    }
}
