using Quartz;
using TeslaSolarCharger.Server.Scheduling.Jobs;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Scheduling;

public class JobManager(
    ILogger<JobManager> logger,
    [FromKeyedServices(JobManager.SchedulerName)] ISchedulerFactory schedulerFactory,
    ISchedulerRuntime schedulerRuntime,
    IConfigurationWrapper configurationWrapper,
    IDateTimeProvider dateTimeProvider,
    ISettings settings,
    IConstants constants)
{
    /// <summary>
    /// Quartz can only build a new scheduler from a registration with a name, see <see cref="GetStartableScheduler"/>.
    /// </summary>
    public const string SchedulerName = "TeslaSolarChargerScheduler";

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
        _scheduler = await GetStartableScheduler().ConfigureAwait(false);

        var chargingValueJob = JobBuilder.Create<ChargingValueJob>().WithIdentity(nameof(ChargingValueJob)).Build();
        var carStateCachingJob = JobBuilder.Create<CarStateCachingJob>().WithIdentity(nameof(CarStateCachingJob)).Build();
        var pvValueJob = JobBuilder.Create<PvValueJob>().WithIdentity(nameof(PvValueJob)).Build();
        var chargingDetailsAddJob = JobBuilder.Create<ChargingDetailsAddJob>().WithIdentity(nameof(ChargingDetailsAddJob)).Build();
        var finishedChargingProcessFinalizingJob = JobBuilder.Create<FinishedChargingProcessFinalizingJob>().WithIdentity(nameof(FinishedChargingProcessFinalizingJob)).Build();
        var mqttReconnectionJob = JobBuilder.Create<MqttReconnectionJob>().WithIdentity(nameof(MqttReconnectionJob)).Build();
        var newVersionCheckJob = JobBuilder.Create<NewVersionCheckJob>().WithIdentity(nameof(NewVersionCheckJob)).Build();
        var spotPriceJob = JobBuilder.Create<SpotPriceJob>().WithIdentity(nameof(SpotPriceJob)).Build();
        var tokenRefreshJob = JobBuilder.Create<TokenRefreshJob>().WithIdentity(nameof(TokenRefreshJob)).Build();
        var vehicleDataRefreshJob = JobBuilder.Create<VehicleDataRefreshJob>().WithIdentity(nameof(VehicleDataRefreshJob)).Build();
        var teslaMateChargeCostUpdateJob = JobBuilder.Create<TeslaMateChargeCostUpdateJob>().WithIdentity(nameof(TeslaMateChargeCostUpdateJob)).Build();
        var backendNotificationRefreshJob = JobBuilder.Create<BackendNotificationRefreshJob>().WithIdentity(nameof(BackendNotificationRefreshJob)).Build();
        var errorMessagingJob = JobBuilder.Create<ErrorMessagingJob>().WithIdentity(nameof(ErrorMessagingJob)).Build();
        var errorDetectionJob = JobBuilder.Create<ErrorDetectionJob>().WithIdentity(nameof(ErrorDetectionJob)).Build();
        var bleApiVersionDetectionJob = JobBuilder.Create<BleApiVersionDetectionJob>().WithIdentity(nameof(BleApiVersionDetectionJob)).Build();
        var fleetTelemetryReconnectionJob = JobBuilder.Create<FleetTelemetryReconnectionJob>().WithIdentity(nameof(FleetTelemetryReconnectionJob)).Build();
        var fleetTelemetryReconfigurationJob = JobBuilder.Create<FleetTelemetryReconfigurationJob>().WithIdentity(nameof(FleetTelemetryReconfigurationJob)).Build();
        var weatherDataRefreshJob = JobBuilder.Create<WeatherDataRefreshJob>().WithIdentity(nameof(WeatherDataRefreshJob)).Build();
        var databaseBufferedValuesSaveJob = JobBuilder.Create<DatabaseBufferedValuesSaveJob>().WithIdentity(nameof(DatabaseBufferedValuesSaveJob)).Build();
        var meterValueMergeJob = JobBuilder.Create<MeterValueMergeJob>().WithIdentity(nameof(MeterValueMergeJob)).Build();
        var homeBatteryMinSocRefreshJob = JobBuilder.Create<HomeBatteryMinSocRefreshJob>().WithIdentity(nameof(HomeBatteryMinSocRefreshJob)).Build();
        var homeBatteryModeJob = JobBuilder.Create<HomeBatteryModeJob>().WithIdentity(nameof(HomeBatteryModeJob)).Build();
        var refreshableValuesRefreshJob = JobBuilder.Create<RefreshableValuesRefreshJob>().WithIdentity(nameof(RefreshableValuesRefreshJob)).Build();
        var manualCarsDataClearingJob = JobBuilder.Create<ManualCarsDataClearingJob>().WithIdentity(nameof(ManualCarsDataClearingJob)).Build();
        var bleDataRefreshJob = JobBuilder.Create<BleDataRefreshJob>().WithIdentity(nameof(BleDataRefreshJob)).Build();

        var currentDate = dateTimeProvider.DateTimeOffSetNow();
        var chargingTriggerStartTime = currentDate.AddSeconds(5);
        var pvTriggerStartTime = currentDate.AddSeconds(3);

        var latestTriggerStartTime = chargingTriggerStartTime;

        var chargingValueJobUpdateIntervall = configurationWrapper.ChargingValueJobUpdateIntervall();

        var chargingValueTrigger = TriggerBuilder.Create()
            .WithIdentity("chargingValueTrigger")
            .StartAt(chargingTriggerStartTime)
            .WithSchedule(RepeatForever(TimeSpan.FromSeconds((int)chargingValueJobUpdateIntervall.TotalSeconds)))
            .Build();

        var bleDataRefreshTrigger = TriggerBuilder.Create()
            .WithIdentity("bleDataRefreshTrigger")
            .StartAt(chargingTriggerStartTime)
            .WithSchedule(RepeatForever(TimeSpan.FromSeconds(configurationWrapper.BleDataRefreshIntervalSeconds())))
            .Build();


        var pvValueJobIntervall = configurationWrapper.PvValueJobUpdateIntervall();
        logger.LogTrace("PvValue Job intervall is {pvValueJobIntervall}", pvValueJobIntervall);

        var pvValueTrigger = TriggerBuilder.Create()
            .WithIdentity("pvValueTrigger")
            .StartAt(pvTriggerStartTime)
            .WithSchedule(RepeatForever(TimeSpan.FromSeconds((int)pvValueJobIntervall.TotalSeconds)))
            .Build();

        var carStateCachingTrigger = TriggerBuilder.Create()
            .WithIdentity("carStateCachingTrigger")
            .StartAt(currentDate.AddMinutes(3))
            .WithSchedule(RepeatForever(TimeSpan.FromMinutes(3))).Build();

        var chargingDetailsAddTrigger = TriggerBuilder.Create().WithIdentity("chargingDetailsAddTrigger")
            .WithSchedule(RepeatForever(TimeSpan.FromSeconds(constants.ChargingDetailsAddTriggerEveryXSeconds))).Build();

        var finishedChargingProcessFinalizingTrigger = TriggerBuilder.Create().WithIdentity("finishedChargingProcessFinalizingTrigger")
            .WithSchedule(RepeatForever(TimeSpan.FromSeconds(118))).Build();

        var mqttReconnectionTrigger = TriggerBuilder.Create().WithIdentity("mqttReconnectionTrigger")
            .WithSchedule(RepeatForever(TimeSpan.FromSeconds(54))).Build();

        var newVersionCheckTrigger = TriggerBuilder.Create().WithIdentity("newVersionCheckTrigger")
            .WithSchedule(RepeatForever(TimeSpan.FromHours(47))).Build();

        var spotPriceRefreshTrigger = TriggerBuilder.Create().WithIdentity("spotPriceRefreshTrigger")
            //Initial loading on startup to ensure no errors occur
            .StartAt(dateTimeProvider.DateTimeOffSetUtcNow().AddHours(constants.SpotPriceRefreshIntervalHours))
            .WithSchedule(RepeatForever(TimeSpan.FromHours(constants.SpotPriceRefreshIntervalHours))).Build();

        var tokenRefreshTrigger = TriggerBuilder.Create().WithIdentity("tokenRefreshTrigger")
            .WithSchedule(RepeatForever(TimeSpan.FromSeconds(constants.TokenRefreshIntervalSeconds))).Build();

        var vehicleDataRefreshTrigger = TriggerBuilder.Create().WithIdentity("vehicleDataRefreshTrigger")
            .WithSchedule(RepeatForever(TimeSpan.FromSeconds(configurationWrapper.CarRefreshAfterCommandSeconds()))).Build();

        var teslaMateChargeCostUpdateTrigger = TriggerBuilder.Create()
            .WithIdentity("teslaMateChargeCostUpdateTrigger")
            //as this creates high CPU load, do it not directly at startup
            .StartAt(dateTimeProvider.DateTimeOffSetNow().AddMinutes(30))
            //When updated, update the helper text in BaseConfigurationBase.cs
            .WithSchedule(RepeatForever(TimeSpan.FromHours(24))).Build();

        var errorMessagingTrigger = TriggerBuilder.Create().WithIdentity("errorMessagingTrigger")
            .WithSchedule(RepeatForever(TimeSpan.FromSeconds(300))).Build();

        var errorDetectionTrigger = TriggerBuilder.Create()
            .WithIdentity("errorDetectionTrigger")
            .StartAt(latestTriggerStartTime.Add(TimeSpan.FromSeconds(12)))
            .WithSchedule(RepeatForever(TimeSpan.FromSeconds(62))).Build();

        var bleApiVersionDetectionTrigger = TriggerBuilder.Create().WithIdentity("bleApiVersionDetectionTrigger")
            .WithSchedule(RepeatForever(TimeSpan.FromSeconds(61))).Build();
        var fleetTelemetryReconnectionTrigger = TriggerBuilder.Create().WithIdentity("fleetTelemetryReconnectionTrigger")
            .StartAt(currentDate.AddSeconds(10))
            .WithSchedule(RepeatForever(TimeSpan.FromSeconds(61))).Build();

        var fleetTelemetryReconfigurationTrigger = TriggerBuilder.Create().WithIdentity("fleetTelemetryReconfigurationTrigger")
            .StartAt(currentDate.AddSeconds(13))
            .WithSchedule(RepeatForever(TimeSpan.FromHours(constants.FleetTelemetryReconfigurationBufferHours))).Build();

        var weatherDataRefreshTrigger = TriggerBuilder.Create().WithIdentity(_weatherDataRefreshTriggerIdentity)
            .StartAt(currentDate.AddSeconds(30))
            .WithSchedule(RepeatForever(TimeSpan.FromHours(constants.WeatherDateRefreshIntervallHours))).Build();

        var databaseBufferedValuesSaveTrigger = TriggerBuilder.Create().WithIdentity("databaseBufferedValuesSaveTrigger")
            .WithSchedule(RepeatForever(TimeSpan.FromMinutes(constants.MeterValueDatabaseSaveIntervalMinutes))).Build();

        var homeBatteryMinSocRefreshTrigger = TriggerBuilder.Create().WithIdentity("homeBatteryMinSocRefreshTrigger")
            //Delay refresh to reduce initial load as many services try to calculate expcted home power and solar values
            .StartAt(currentDate.Add(TimeSpan.FromMinutes(1)))
            .WithSchedule(RepeatForever(TimeSpan.FromMinutes(constants.HomeBatteryMinSocRefreshIntervalMinutes))).Build();

        var homeBatteryModeTrigger = TriggerBuilder.Create().WithIdentity("homeBatteryModeTrigger")
            //Delay start so solar values are available before the first mode evaluation
            .StartAt(currentDate.AddSeconds(20))
            .WithSchedule(RepeatForever(TimeSpan.FromSeconds(constants.HomeBatteryModeJobIntervalSeconds))).Build();

        var refreshableValuesRefreshTrigger = TriggerBuilder.Create().WithIdentity("refreshableValuesRefreshTrigger")
            .WithSchedule(RepeatForever(TimeSpan.FromSeconds(constants.RefreshableValuesRefreshIntervalSeconds))).Build();
        var manualCarsDataClearingTrigger = TriggerBuilder.Create().WithIdentity("manualCarsDataClearingTrigger")
            .StartAt(currentDate.AddMinutes(constants.ManualCarMinutesUntilForgetSoc + 1))
            .WithSchedule(RepeatForever(TimeSpan.FromMinutes(constants.ManualCarMinutesUntilForgetSoc))).Build();

        var random = new Random();
        var hour = random.Next(0, 5);
        var minute = random.Next(0, 59);

        var triggerAtNight = TriggerBuilder.Create().WithIdentity("triggerAtNight")
            .WithSchedule(DailyAtHourAndMinute(hour, minute).InTimeZone(TimeZoneInfo.Local))// Run every day at 0:00 UTC
            .StartNow()
            .Build();

        // Create a separate daily trigger for meter value merge, at a different random time to spread load
        var mergeHour = random.Next(1, 6); // Different range to avoid collision
        var mergeMinute = random.Next(0, 59);
        var meterValueMergeTrigger = TriggerBuilder.Create()
            .WithIdentity("meterValueMergeTrigger")
            .WithSchedule(DailyAtHourAndMinute(mergeHour, mergeMinute).InTimeZone(TimeZoneInfo.Local))
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
            {spotPriceJob, new HashSet<ITrigger> {spotPriceRefreshTrigger}},
            {errorMessagingJob, new HashSet<ITrigger> {errorMessagingTrigger}},
            {errorDetectionJob, new HashSet<ITrigger> {errorDetectionTrigger}},
            {bleApiVersionDetectionJob, new HashSet<ITrigger> {bleApiVersionDetectionTrigger}},
            {fleetTelemetryReconnectionJob, new HashSet<ITrigger> {fleetTelemetryReconnectionTrigger}},
            {fleetTelemetryReconfigurationJob, new HashSet<ITrigger> {fleetTelemetryReconfigurationTrigger}},
            {weatherDataRefreshJob, new HashSet<ITrigger> {weatherDataRefreshTrigger}},
            {databaseBufferedValuesSaveJob, new HashSet<ITrigger> {databaseBufferedValuesSaveTrigger}},
            {meterValueMergeJob, new HashSet<ITrigger> {meterValueMergeTrigger}},
            {homeBatteryMinSocRefreshJob, new HashSet<ITrigger> {homeBatteryMinSocRefreshTrigger}},
            {homeBatteryModeJob, new HashSet<ITrigger> {homeBatteryModeTrigger}},
            {refreshableValuesRefreshJob, new HashSet<ITrigger> {refreshableValuesRefreshTrigger}},
            {manualCarsDataClearingJob, new HashSet<ITrigger> {manualCarsDataClearingTrigger}},
        };

        if (!configurationWrapper.ShouldUseFakeSolarValues())
        {
            triggersAndJobs.Add(chargingValueJob, new HashSet<ITrigger> { chargingValueTrigger });
            triggersAndJobs.Add(bleDataRefreshJob, new HashSet<ITrigger> { bleDataRefreshTrigger });
            triggersAndJobs.Add(carStateCachingJob, new HashSet<ITrigger> { carStateCachingTrigger });
            triggersAndJobs.Add(finishedChargingProcessFinalizingJob, new HashSet<ITrigger> { finishedChargingProcessFinalizingTrigger });
            triggersAndJobs.Add(mqttReconnectionJob, new HashSet<ITrigger> { mqttReconnectionTrigger });
            triggersAndJobs.Add(tokenRefreshJob, new HashSet<ITrigger> { tokenRefreshTrigger });
            triggersAndJobs.Add(vehicleDataRefreshJob, new HashSet<ITrigger> { vehicleDataRefreshTrigger });
            triggersAndJobs.Add(teslaMateChargeCostUpdateJob, new HashSet<ITrigger> { teslaMateChargeCostUpdateTrigger });
            triggersAndJobs.Add(backendNotificationRefreshJob, new HashSet<ITrigger> { triggerAtNight, triggerNow });
        }

        await _scheduler.ScheduleJobs(triggersAndJobs).ConfigureAwait(false);

        await _scheduler.Start().ConfigureAwait(false);
    }

    private async Task<IScheduler> GetStartableScheduler()
    {
        if (_scheduler?.Status == SchedulerStatus.Shutdown)
        {
            // A Quartz scheduler can not be started again after StopJobs shut it down, so build a new one from the same registration.
            logger.LogDebug("Building a new scheduler as the previous one was shut down.");
            await schedulerRuntime.Restart(SchedulerName).ConfigureAwait(false);
        }
        return await schedulerFactory.GetScheduler().ConfigureAwait(false);
    }

    private static SimpleScheduleBuilder RepeatForever(TimeSpan interval) =>
        SimpleScheduleBuilder.Create().WithInterval(interval).RepeatForever();

    private static CronScheduleBuilder DailyAtHourAndMinute(int hour, int minute) =>
        CronScheduleBuilder.Create($"0 {minute} {hour} ? * *");

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
            var nextFiretime = trigger.NextFireTimeUtc;
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
