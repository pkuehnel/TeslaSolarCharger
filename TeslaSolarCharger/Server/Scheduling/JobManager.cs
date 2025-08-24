using Quartz;
using Quartz.Spi;
using TeslaSolarCharger.Server.Scheduling.Jobs;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Scheduling;

public class JobManager(
    ILogger<JobManager> logger,
    IJobFactory jobFactory,
    ISchedulerFactory schedulerFactory,
    IConfigurationWrapper configurationWrapper,
    IDateTimeProvider dateTimeProvider,
    ISettings settings,
    IConstants constants)
{
    private IScheduler? _scheduler;
    private readonly string _weatherDataRefreshTriggerIdentity = "weatherDataRefreshTrigger";


    public async Task StartJobs()
    {
        logger.LogTrace("{Method}()", nameof(StartJobs));
        if (settings.RestartNeeded)
        {
            logger.LogError("Do not start jobs as application restart is needed.");
            return;
        }
        if (settings.CrashedOnStartup)
        {
            logger.LogError("Do not start jobs as application crashed during startup.");
            return;
        }
        _scheduler = schedulerFactory.GetScheduler().GetAwaiter().GetResult();
        _scheduler.JobFactory = jobFactory;

        var chargingValueJob = JobBuilder.Create<ChargingValueJob>().WithIdentity(nameof(ChargingValueJob)).Build();
        var carStateCachingJob = JobBuilder.Create<CarStateCachingJob>().WithIdentity(nameof(CarStateCachingJob)).Build();
        var pvValueJob = JobBuilder.Create<PvValueJob>().WithIdentity(nameof(PvValueJob)).Build();
        var chargingDetailsAddJob = JobBuilder.Create<ChargingDetailsAddJob>().WithIdentity(nameof(ChargingDetailsAddJob)).Build();
        var finishedChargingProcessFinalizingJob = JobBuilder.Create<FinishedChargingProcessFinalizingJob>().WithIdentity(nameof(FinishedChargingProcessFinalizingJob)).Build();
        var mqttReconnectionJob = JobBuilder.Create<MqttReconnectionJob>().WithIdentity(nameof(MqttReconnectionJob)).Build();
        var newVersionCheckJob = JobBuilder.Create<NewVersionCheckJob>().WithIdentity(nameof(NewVersionCheckJob)).Build();
        var spotPriceJob = JobBuilder.Create<SpotPriceJob>().WithIdentity(nameof(SpotPriceJob)).Build();
        var backendTokenRefreshJob = JobBuilder.Create<BackendTokenRefreshJob>().WithIdentity(nameof(BackendTokenRefreshJob)).Build();
        var fleetApiTokenRefreshJob = JobBuilder.Create<FleetApiTokenRefreshJob>().WithIdentity(nameof(FleetApiTokenRefreshJob)).Build();
        var vehicleDataRefreshJob = JobBuilder.Create<VehicleDataRefreshJob>().WithIdentity(nameof(VehicleDataRefreshJob)).Build();
        var teslaMateChargeCostUpdateJob = JobBuilder.Create<TeslaMateChargeCostUpdateJob>().WithIdentity(nameof(TeslaMateChargeCostUpdateJob)).Build();
        var backendNotificationRefreshJob = JobBuilder.Create<BackendNotificationRefreshJob>().WithIdentity(nameof(BackendNotificationRefreshJob)).Build();
        var errorMessagingJob = JobBuilder.Create<ErrorMessagingJob>().WithIdentity(nameof(ErrorMessagingJob)).Build();
        var errorDetectionJob = JobBuilder.Create<ErrorDetectionJob>().WithIdentity(nameof(ErrorDetectionJob)).Build();
        var bleApiVersionDetectionJob = JobBuilder.Create<BleApiVersionDetectionJob>().WithIdentity(nameof(BleApiVersionDetectionJob)).Build();
        var fleetTelemetryReconnectionJob = JobBuilder.Create<FleetTelemetryReconnectionJob>().WithIdentity(nameof(FleetTelemetryReconnectionJob)).Build();
        var fleetTelemetryReconfigurationJob = JobBuilder.Create<FleetTelemetryReconfigurationJob>().WithIdentity(nameof(FleetTelemetryReconfigurationJob)).Build();
        var weatherDataRefreshJob = JobBuilder.Create<WeatherDataRefreshJob>().WithIdentity(nameof(WeatherDataRefreshJob)).Build();
        var meterValueDatabaseSaveJob = JobBuilder.Create<MeterValueDatabaseSaveJob>().WithIdentity(nameof(MeterValueDatabaseSaveJob)).Build();
        var meterValueMergeJob = JobBuilder.Create<MeterValueMergeJob>().WithIdentity(nameof(MeterValueMergeJob)).Build();
        var homeBatteryMinSocRefreshJob = JobBuilder.Create<HomeBatteryMinSocRefreshJob>().WithIdentity(nameof(HomeBatteryMinSocRefreshJob)).Build();

        var currentDate = dateTimeProvider.DateTimeOffSetNow();
        var chargingTriggerStartTime = currentDate.AddSeconds(5);
        var pvTriggerStartTime = currentDate.AddSeconds(3);

        var latestTriggerStartTime = chargingTriggerStartTime;

        var chargingValueJobUpdateIntervall = configurationWrapper.ChargingValueJobUpdateIntervall();

        var chargingValueTrigger = TriggerBuilder.Create()
            .WithIdentity("chargingValueTrigger")
            .StartAt(chargingTriggerStartTime)
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever((int)chargingValueJobUpdateIntervall.TotalSeconds))
            .Build();


        var pvValueJobIntervall = configurationWrapper.PvValueJobUpdateIntervall();
        logger.LogTrace("PvValue Job intervall is {pvValueJobIntervall}", pvValueJobIntervall);

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
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever(constants.ChargingDetailsAddTriggerEveryXSeconds)).Build();

        var finishedChargingProcessFinalizingTrigger = TriggerBuilder.Create().WithIdentity("finishedChargingProcessFinalizingTrigger")
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever(118)).Build();

        var mqttReconnectionTrigger = TriggerBuilder.Create().WithIdentity("mqttReconnectionTrigger")
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever(54)).Build();

        var newVersionCheckTrigger = TriggerBuilder.Create().WithIdentity("newVersionCheckTrigger")
            .WithSchedule(SimpleScheduleBuilder.RepeatHourlyForever(47)).Build();

        var spotPricePlanningTrigger = TriggerBuilder.Create().WithIdentity("spotPricePlanningTrigger")
            .WithSchedule(SimpleScheduleBuilder.RepeatHourlyForever(1)).Build();

        var backendTokenRefreshTrigger = TriggerBuilder.Create().WithIdentity("backendTokenRefreshTrigger")
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever(59)).Build();

        var fleetApiTokenRefreshTrigger = TriggerBuilder.Create().WithIdentity("fleetApiTokenRefreshTrigger")
            //start 5 seconds later, so backend token is already refreshed
            .StartAt(currentDate.AddSeconds(5))
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever(58)).Build();

        var vehicleDataRefreshTrigger = TriggerBuilder.Create().WithIdentity("vehicleDataRefreshTrigger")
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever(configurationWrapper.CarRefreshAfterCommandSeconds())).Build();

        var teslaMateChargeCostUpdateTrigger = TriggerBuilder.Create()
            .WithIdentity("teslaMateChargeCostUpdateTrigger")
            //as this creates high CPU load, do it not directly at startup
            .StartAt(dateTimeProvider.DateTimeOffSetNow().AddMinutes(30))
            //When updated, update the helper text in BaseConfigurationBase.cs
            .WithSchedule(SimpleScheduleBuilder.RepeatHourlyForever(24)).Build();

        var errorMessagingTrigger = TriggerBuilder.Create().WithIdentity("errorMessagingTrigger")
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever(300)).Build();

        var errorDetectionTrigger = TriggerBuilder.Create()
            .WithIdentity("errorDetectionTrigger")
            .StartAt(latestTriggerStartTime.Add(TimeSpan.FromSeconds(12)))
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever(62)).Build();

        var bleApiVersionDetectionTrigger = TriggerBuilder.Create().WithIdentity("bleApiVersionDetectionTrigger")
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever(61)).Build();
        var fleetTelemetryReconnectionTrigger = TriggerBuilder.Create().WithIdentity("fleetTelemetryReconnectionTrigger")
            .StartAt(currentDate.AddSeconds(10))
            .WithSchedule(SimpleScheduleBuilder.RepeatSecondlyForever(61)).Build();

        var fleetTelemetryReconfigurationTrigger = TriggerBuilder.Create().WithIdentity("fleetTelemetryReconfigurationTrigger")
            .StartAt(currentDate.AddSeconds(13))
            .WithSchedule(SimpleScheduleBuilder.RepeatHourlyForever(constants.FleetTelemetryReconfigurationBufferHours)).Build();

        var weatherDataRefreshTrigger = TriggerBuilder.Create().WithIdentity(_weatherDataRefreshTriggerIdentity)
            .StartAt(currentDate.AddSeconds(30))
            .WithSchedule(SimpleScheduleBuilder.RepeatHourlyForever(constants.WeatherDateRefreshIntervallHours)).Build();

        var meterValueDatabaseSaveTrigger = TriggerBuilder.Create().WithIdentity("meterValueDatabaseSaveTrigger")
            .WithSchedule(SimpleScheduleBuilder.RepeatMinutelyForever(constants.MeterValueDatabaseSaveIntervalMinutes)).Build();

        var homeBatteryMinSocRefreshTrigger = TriggerBuilder.Create().WithIdentity("homeBatteryMinSocRefreshTrigger")
            //Delay refresh to reduce initial load as many services try to calculate expcted home power and solar values
            .StartAt(currentDate.Add(TimeSpan.FromMinutes(7)))
            .WithSchedule(SimpleScheduleBuilder.RepeatMinutelyForever(constants.HomeBatteryMinSocRefreshIntervalMinutes)).Build();

        var random = new Random();
        var hour = random.Next(0, 5);
        var minute = random.Next(0, 59);

        var triggerAtNight = TriggerBuilder.Create().WithIdentity("triggerAtNight")
            .WithSchedule(CronScheduleBuilder.DailyAtHourAndMinute(hour, minute).InTimeZone(TimeZoneInfo.Local))// Run every day at 0:00 UTC
            .StartNow()
            .Build();

        // Create a separate daily trigger for meter value merge, at a different random time to spread load
        var mergeHour = random.Next(1, 6); // Different range to avoid collision
        var mergeMinute = random.Next(0, 59);
        var meterValueMergeTrigger = TriggerBuilder.Create()
            .WithIdentity("meterValueMergeTrigger")
            .WithSchedule(CronScheduleBuilder.DailyAtHourAndMinute(mergeHour, mergeMinute).InTimeZone(TimeZoneInfo.Local))
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
            {errorMessagingJob, new HashSet<ITrigger> {errorMessagingTrigger}},
            {errorDetectionJob, new HashSet<ITrigger> {errorDetectionTrigger}},
            {bleApiVersionDetectionJob, new HashSet<ITrigger> {bleApiVersionDetectionTrigger}},
            {fleetTelemetryReconnectionJob, new HashSet<ITrigger> {fleetTelemetryReconnectionTrigger}},
            {fleetTelemetryReconfigurationJob, new HashSet<ITrigger> {fleetTelemetryReconfigurationTrigger}},
            {weatherDataRefreshJob, new HashSet<ITrigger> {weatherDataRefreshTrigger}},
            {meterValueDatabaseSaveJob, new HashSet<ITrigger> {meterValueDatabaseSaveTrigger}},
            {meterValueMergeJob, new HashSet<ITrigger> {meterValueMergeTrigger}},
            {homeBatteryMinSocRefreshJob, new HashSet<ITrigger> {homeBatteryMinSocRefreshTrigger}},
        };

        if (!configurationWrapper.ShouldUseFakeSolarValues())
        {
            triggersAndJobs.Add(chargingValueJob, new HashSet<ITrigger> { chargingValueTrigger });
            triggersAndJobs.Add(carStateCachingJob, new HashSet<ITrigger> { carStateCachingTrigger });
            triggersAndJobs.Add(finishedChargingProcessFinalizingJob, new HashSet<ITrigger> { finishedChargingProcessFinalizingTrigger });
            triggersAndJobs.Add(mqttReconnectionJob, new HashSet<ITrigger> { mqttReconnectionTrigger });
            triggersAndJobs.Add(backendTokenRefreshJob, new HashSet<ITrigger> { backendTokenRefreshTrigger });
            triggersAndJobs.Add(fleetApiTokenRefreshJob, new HashSet<ITrigger> { fleetApiTokenRefreshTrigger });
            triggersAndJobs.Add(vehicleDataRefreshJob, new HashSet<ITrigger> { vehicleDataRefreshTrigger });
            triggersAndJobs.Add(teslaMateChargeCostUpdateJob, new HashSet<ITrigger> { teslaMateChargeCostUpdateTrigger });
            triggersAndJobs.Add(backendNotificationRefreshJob, new HashSet<ITrigger> { triggerAtNight, triggerNow });
        }

        await _scheduler.ScheduleJobs(triggersAndJobs, false).ConfigureAwait(false);

        await _scheduler.Start().ConfigureAwait(false);
    }

    public async Task<DateTimeOffset?> GetWeatherDataRefreshNextFireTimeAsync()
    {
        logger.LogTrace("{method}()", nameof(GetWeatherDataRefreshNextFireTimeAsync));
        // Create the TriggerKey using the same identity as used in JobManager
        var triggerKey = new TriggerKey(_weatherDataRefreshTriggerIdentity);

        if (_scheduler == default)
        {
            logger.LogError("Scheduler is not initialized.");
            return default;
        }
        // Retrieve the trigger from the scheduler
        var trigger = await _scheduler.GetTrigger(triggerKey);

        if (trigger != null)
        {
            
            // Return the next scheduled fire time in UTC
            var nextFiretime = trigger.GetNextFireTimeUtc();
            logger.LogTrace("Trigger {triggername} found, next firetime: {nextFireTime}", _weatherDataRefreshTriggerIdentity, nextFiretime);
            return nextFiretime;
        }

        // If the trigger isn't found, you could log or handle this case as needed.
        logger.LogError("Trigger {triggername} not found.", _weatherDataRefreshTriggerIdentity);
        return default;
    }

    public async Task<bool> StopJobs()
    {
        logger.LogTrace("{method}()", nameof(StopJobs));
        if (_scheduler == null)
        {
            logger.LogInformation("Jobs were not running, yet, so stop is not needed.");
            return false;
        }
        await _scheduler.Shutdown(true).ConfigureAwait(false);
        return true;
    }
}
