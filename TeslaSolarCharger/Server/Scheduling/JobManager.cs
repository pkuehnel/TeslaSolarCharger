using Quartz;
using Quartz.Spi;
using TeslaSolarCharger.Server.Scheduling.Jobs;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Scheduling;

public class JobManager
{
    private readonly ILogger<JobManager> _logger;
    private readonly IJobFactory _jobFactory;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IConfigurationWrapper _configurationWrapper;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISettings _settings;
    private readonly IConstants _constants;

    private IScheduler? _scheduler;


#pragma warning disable CS8618
    public JobManager(ILogger<JobManager> logger, IJobFactory jobFactory, ISchedulerFactory schedulerFactory,
        IConfigurationWrapper configurationWrapper, IDateTimeProvider dateTimeProvider, ISettings settings, IConstants constants)
#pragma warning restore CS8618
    {
        _logger = logger;
        _jobFactory = jobFactory;
        _schedulerFactory = schedulerFactory;
        _configurationWrapper = configurationWrapper;
        _dateTimeProvider = dateTimeProvider;
        _settings = settings;
        _constants = constants;
    }

    public async Task StartJobs()
    {
        _logger.LogTrace("{Method}()", nameof(StartJobs));
        if (_settings.RestartNeeded)
        {
            _logger.LogError("Do not start jobs as application restart is needed.");
            return;
        }
        if (_settings.CrashedOnStartup)
        {
            _logger.LogError("Do not start jobs as application crashed during startup.");
            return;
        }
        _scheduler = _schedulerFactory.GetScheduler().GetAwaiter().GetResult();
        _scheduler.JobFactory = _jobFactory;

        var chargingValueJob = JobBuilder.Create<ChargingValueJob>().WithIdentity(nameof(ChargingValueJob)).Build();
        var carStateCachingJob = JobBuilder.Create<CarStateCachingJob>().WithIdentity(nameof(CarStateCachingJob)).Build();
        var pvValueJob = JobBuilder.Create<PvValueJob>().WithIdentity(nameof(PvValueJob)).Build();
        var chargingDetailsAddJob = JobBuilder.Create<ChargingDetailsAddJob>().WithIdentity(nameof(ChargingDetailsAddJob)).Build();
        var finishedChargingProcessFinalizingJob = JobBuilder.Create<FinishedChargingProcessFinalizingJob>().WithIdentity(nameof(FinishedChargingProcessFinalizingJob)).Build();
        var mqttReconnectionJob = JobBuilder.Create<MqttReconnectionJob>().WithIdentity(nameof(MqttReconnectionJob)).Build();
        var newVersionCheckJob = JobBuilder.Create<NewVersionCheckJob>().WithIdentity(nameof(NewVersionCheckJob)).Build();
        var spotPriceJob = JobBuilder.Create<SpotPriceJob>().WithIdentity(nameof(SpotPriceJob)).Build();
        var fleetApiTokenRefreshJob = JobBuilder.Create<FleetApiTokenRefreshJob>().WithIdentity(nameof(FleetApiTokenRefreshJob)).Build();
        var vehicleDataRefreshJob = JobBuilder.Create<VehicleDataRefreshJob>().WithIdentity(nameof(VehicleDataRefreshJob)).Build();
        var teslaMateChargeCostUpdateJob = JobBuilder.Create<TeslaMateChargeCostUpdateJob>().WithIdentity(nameof(TeslaMateChargeCostUpdateJob)).Build();
        var apiCallCounterResetJob = JobBuilder.Create<ApiCallCounterResetJob>().WithIdentity(nameof(ApiCallCounterResetJob)).Build();

        var currentDate = _dateTimeProvider.DateTimeOffSetNow();
        var chargingTriggerStartTime = currentDate.AddSeconds(5);
        var pvTriggerStartTime = currentDate.AddSeconds(3);

        var chargingValueJobUpdateIntervall = _configurationWrapper.ChargingValueJobUpdateIntervall();

        var chargingValueTrigger = TriggerBuilder.Create()
            .WithIdentity("chargingValueTrigger")
            .StartAt(chargingTriggerStartTime)
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever((int)chargingValueJobUpdateIntervall.TotalSeconds))
            .Build();


        var pvValueJobIntervall = _configurationWrapper.PvValueJobUpdateIntervall();
        _logger.LogTrace("PvValue Job intervall is {pvValueJobIntervall}", pvValueJobIntervall);

        var pvValueTrigger = TriggerBuilder.Create()
            .WithIdentity("pvValueTrigger")
            .StartAt(pvTriggerStartTime)
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever((int)pvValueJobIntervall.TotalSeconds))
            .Build();

        var carStateCachingTrigger = TriggerBuilder.Create()
            .WithIdentity("carStateCachingTrigger")
            .StartAt(currentDate.AddMinutes(3))
            .WithSchedule(SimpleScheduleBuilder.RepeatMinutelyForever(3)).Build();

        var chargingDetailsAddTrigger = TriggerBuilder.Create().WithIdentity("chargingDetailsAddTrigger")
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever(_constants.ChargingDetailsAddTriggerEveryXSeconds)).Build();

        var finishedChargingProcessFinalizingTrigger = TriggerBuilder.Create().WithIdentity("finishedChargingProcessFinalizingTrigger")
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever(118)).Build();

        var mqttReconnectionTrigger = TriggerBuilder.Create().WithIdentity("mqttReconnectionTrigger")
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever(54)).Build();

        var newVersionCheckTrigger = TriggerBuilder.Create().WithIdentity("newVersionCheckTrigger")
            .WithSchedule(SimpleScheduleBuilder.RepeatHourlyForever(47)).Build();

        var spotPricePlanningTrigger = TriggerBuilder.Create().WithIdentity("spotPricePlanningTrigger")
            .WithSchedule(SimpleScheduleBuilder.RepeatHourlyForever(1)).Build();

        var fleetApiTokenRefreshTrigger = TriggerBuilder.Create().WithIdentity("fleetApiTokenRefreshTrigger")
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever(59)).Build();

        var vehicleDataRefreshTrigger = TriggerBuilder.Create().WithIdentity("vehicleDataRefreshTrigger")
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever(11)).Build();

        var teslaMateChargeCostUpdateTrigger = TriggerBuilder.Create().WithIdentity("teslaMateChargeCostUpdateTrigger")
            .WithSchedule(SimpleScheduleBuilder.RepeatHourlyForever(24)).Build();

        var random = new Random();
        var hour = random.Next(0, 5);
        var minute = random.Next(0, 59);

        var triggerAtNight = TriggerBuilder.Create().WithIdentity("triggerAtNight")
            .WithSchedule(CronScheduleBuilder.DailyAtHourAndMinute(hour, minute).InTimeZone(TimeZoneInfo.Utc))// Run every day at 0:00 UTC
            .StartNow()
            .Build();

        var triggerNow = TriggerBuilder
            .Create().WithIdentity("triggerNow")
            .StartAt(DateTimeOffset.Now.AddSeconds(15))
            .Build();

        var triggersAndJobs = new Dictionary<IJobDetail, IReadOnlyCollection<ITrigger>>
        {
            {pvValueJob, new HashSet<ITrigger> {pvValueTrigger}},
            {chargingDetailsAddJob, new HashSet<ITrigger> {chargingDetailsAddTrigger}},
            {newVersionCheckJob, new HashSet<ITrigger> {newVersionCheckTrigger}},
            {spotPriceJob, new HashSet<ITrigger> {spotPricePlanningTrigger}},
        };

        if (!_configurationWrapper.ShouldUseFakeSolarValues())
        {
            triggersAndJobs.Add(chargingValueJob, new HashSet<ITrigger> { chargingValueTrigger });
            triggersAndJobs.Add(carStateCachingJob, new HashSet<ITrigger> { carStateCachingTrigger });
            triggersAndJobs.Add(finishedChargingProcessFinalizingJob, new HashSet<ITrigger> { finishedChargingProcessFinalizingTrigger });
            triggersAndJobs.Add(mqttReconnectionJob, new HashSet<ITrigger> { mqttReconnectionTrigger });
            triggersAndJobs.Add(fleetApiTokenRefreshJob, new HashSet<ITrigger> { fleetApiTokenRefreshTrigger });
            triggersAndJobs.Add(vehicleDataRefreshJob, new HashSet<ITrigger> { vehicleDataRefreshTrigger });
            triggersAndJobs.Add(teslaMateChargeCostUpdateJob, new HashSet<ITrigger> { teslaMateChargeCostUpdateTrigger });
            triggersAndJobs.Add(apiCallCounterResetJob, new HashSet<ITrigger> { triggerAtNight, triggerNow });
        }

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
