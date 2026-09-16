using Microsoft.Extensions.DependencyInjection;
using Moq;
using Quartz;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeslaSolarCharger.Server;
using TeslaSolarCharger.Server.Scheduling;
using TeslaSolarCharger.Server.Scheduling.Jobs;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Resources.Contracts;
using Xunit;

namespace TeslaSolarCharger.Tests.Services.Server;

public class JobManagerTests(ITestOutputHelper outputHelper) : TestBase(outputHelper)
{
    private static readonly TimeSpan ChargingValueJobUpdateInterval = TimeSpan.FromSeconds(30.9);
    private static readonly TimeSpan PvValueJobUpdateInterval = TimeSpan.FromSeconds(2);
    private const int BleDataRefreshIntervalSeconds = 20;
    private const int CarRefreshAfterCommandSeconds = 13;

    private static readonly string[] CarAndChargingJobNames =
    [
        nameof(ChargingValueJob),
        nameof(BleDataRefreshJob),
        nameof(CarStateCachingJob),
        nameof(FinishedChargingProcessFinalizingJob),
        nameof(MqttReconnectionJob),
        nameof(TokenRefreshJob),
        nameof(VehicleDataRefreshJob),
        nameof(TeslaMateChargeCostUpdateJob),
        nameof(BackendNotificationRefreshJob),
    ];

    private readonly DateTimeOffset _now = new(DateTimeOffset.UtcNow.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, TimeSpan.Zero);

    [Fact]
    public async Task StartJobs_SchedulesEveryJobWithItsRepeatInterval()
    {
        await using var provider = BuildServiceProvider();
        var constants = provider.GetRequiredService<IConstants>();
        var jobManager = provider.GetRequiredService<JobManager>();

        await jobManager.StartJobs();

        var scheduler = await GetScheduler(provider);
        Assert.Equal(SchedulerStatus.Running, scheduler.Status);
        var expectedRepeatIntervals = new Dictionary<string, (string JobName, TimeSpan Interval)>
        {
            { "pvValueTrigger", (nameof(PvValueJob), PvValueJobUpdateInterval) },
            { "chargingDetailsAddTrigger", (nameof(ChargingDetailsAddJob), TimeSpan.FromSeconds(constants.ChargingDetailsAddTriggerEveryXSeconds)) },
            { "newVersionCheckTrigger", (nameof(NewVersionCheckJob), TimeSpan.FromHours(47)) },
            { "spotPriceRefreshTrigger", (nameof(SpotPriceJob), TimeSpan.FromHours(constants.SpotPriceRefreshIntervalHours)) },
            { "errorMessagingTrigger", (nameof(ErrorMessagingJob), TimeSpan.FromSeconds(300)) },
            { "errorDetectionTrigger", (nameof(ErrorDetectionJob), TimeSpan.FromSeconds(62)) },
            { "bleApiVersionDetectionTrigger", (nameof(BleApiVersionDetectionJob), TimeSpan.FromSeconds(61)) },
            { "fleetTelemetryReconnectionTrigger", (nameof(FleetTelemetryReconnectionJob), TimeSpan.FromSeconds(61)) },
            { "fleetTelemetryReconfigurationTrigger", (nameof(FleetTelemetryReconfigurationJob), TimeSpan.FromHours(constants.FleetTelemetryReconfigurationBufferHours)) },
            { "weatherDataRefreshTrigger", (nameof(WeatherDataRefreshJob), TimeSpan.FromHours(constants.WeatherDateRefreshIntervallHours)) },
            { "databaseBufferedValuesSaveTrigger", (nameof(DatabaseBufferedValuesSaveJob), TimeSpan.FromMinutes(constants.MeterValueDatabaseSaveIntervalMinutes)) },
            { "homeBatteryMinSocRefreshTrigger", (nameof(HomeBatteryMinSocRefreshJob), TimeSpan.FromMinutes(constants.HomeBatteryMinSocRefreshIntervalMinutes)) },
            { "homeBatteryModeTrigger", (nameof(HomeBatteryModeJob), TimeSpan.FromSeconds(constants.HomeBatteryModeJobIntervalSeconds)) },
            { "refreshableValuesRefreshTrigger", (nameof(RefreshableValuesRefreshJob), TimeSpan.FromSeconds(constants.RefreshableValuesRefreshIntervalSeconds)) },
            { "manualCarsDataClearingTrigger", (nameof(ManualCarsDataClearingJob), TimeSpan.FromMinutes(constants.ManualCarMinutesUntilForgetSoc)) },
            // Sub second parts of the configured interval are cut off, as they were before Quartz 4.
            { "chargingValueTrigger", (nameof(ChargingValueJob), TimeSpan.FromSeconds(30)) },
            { "bleDataRefreshTrigger", (nameof(BleDataRefreshJob), TimeSpan.FromSeconds(BleDataRefreshIntervalSeconds)) },
            { "carStateCachingTrigger", (nameof(CarStateCachingJob), TimeSpan.FromMinutes(3)) },
            { "finishedChargingProcessFinalizingTrigger", (nameof(FinishedChargingProcessFinalizingJob), TimeSpan.FromSeconds(118)) },
            { "mqttReconnectionTrigger", (nameof(MqttReconnectionJob), TimeSpan.FromSeconds(54)) },
            { "tokenRefreshTrigger", (nameof(TokenRefreshJob), TimeSpan.FromSeconds(constants.TokenRefreshIntervalSeconds)) },
            { "vehicleDataRefreshTrigger", (nameof(VehicleDataRefreshJob), TimeSpan.FromSeconds(CarRefreshAfterCommandSeconds)) },
            { "teslaMateChargeCostUpdateTrigger", (nameof(TeslaMateChargeCostUpdateJob), TimeSpan.FromHours(24)) },
        };
        foreach (var (triggerName, (jobName, interval)) in expectedRepeatIntervals)
        {
            var trigger = Assert.IsAssignableFrom<ISimpleTrigger>(await scheduler.GetTrigger(new TriggerKey(triggerName)));
            Assert.Equal(jobName, trigger.JobKey.Name);
            Assert.Equal(interval, trigger.RepeatInterval);
            Assert.Equal(-1, trigger.RepeatCount);
        }

        foreach (var (triggerName, jobName) in new[] { ("triggerAtNight", nameof(BackendNotificationRefreshJob)), ("meterValueMergeTrigger", nameof(MeterValueMergeJob)), })
        {
            var trigger = Assert.IsAssignableFrom<ICronTrigger>(await scheduler.GetTrigger(new TriggerKey(triggerName)));
            Assert.Equal(jobName, trigger.JobKey.Name);
            Assert.Matches(@"^0 \d{1,2} \d \? \* \*$", trigger.CronExpressionString);
            Assert.Equal(TimeZoneInfo.Local, trigger.TimeZone);
        }

        var triggerNow = Assert.IsAssignableFrom<ISimpleTrigger>(await scheduler.GetTrigger(new TriggerKey("triggerNow")));
        Assert.Equal(nameof(BackendNotificationRefreshJob), triggerNow.JobKey.Name);
        Assert.Equal(0, triggerNow.RepeatCount);

        var jobTypeNames = typeof(JobManager).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, } && typeof(IJob).IsAssignableFrom(t))
            .Select(t => t.Name);
        Assert.Equal(jobTypeNames.Order(), await GetScheduledJobNames(scheduler));
    }

    [Fact]
    public async Task StartJobs_FakeSolarValues_DoesNotScheduleCarAndChargingJobs()
    {
        await using var provider = BuildServiceProvider(shouldUseFakeSolarValues: true);
        var jobManager = provider.GetRequiredService<JobManager>();

        await jobManager.StartJobs();

        var scheduledJobNames = await GetScheduledJobNames(await GetScheduler(provider));
        Assert.Equal(16, scheduledJobNames.Count);
        Assert.Empty(scheduledJobNames.Intersect(CarAndChargingJobNames));
    }

    [Fact]
    public async Task GetWeatherDataRefreshNextFireTimeAsync_AfterStart_ReturnsDelayedFirstFireTime()
    {
        await using var provider = BuildServiceProvider();
        var jobManager = provider.GetRequiredService<JobManager>();
        await jobManager.StartJobs();

        var nextFireTime = await jobManager.GetWeatherDataRefreshNextFireTimeAsync();

        Assert.Equal(_now.AddSeconds(30), nextFireTime);
    }

    [Fact]
    public async Task GetWeatherDataRefreshNextFireTimeAsync_BeforeStart_ReturnsNull()
    {
        await using var provider = BuildServiceProvider();
        var jobManager = provider.GetRequiredService<JobManager>();

        Assert.Null(await jobManager.GetWeatherDataRefreshNextFireTimeAsync());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task StartJobs_RestartNeededOrCrashedOnStartup_DoesNotStartJobs(bool restartNeeded, bool crashedOnStartup)
    {
        await using var provider = BuildServiceProvider(restartNeeded: restartNeeded, crashedOnStartup: crashedOnStartup);
        var jobManager = provider.GetRequiredService<JobManager>();

        await jobManager.StartJobs();

        Assert.Null(await jobManager.GetWeatherDataRefreshNextFireTimeAsync());
        Assert.False(await jobManager.StopJobs());
        Assert.Equal(SchedulerStatus.Created, (await GetScheduler(provider)).Status);
    }

    [Fact]
    public async Task StopJobs_BeforeStart_ReturnsFalse()
    {
        await using var provider = BuildServiceProvider();
        var jobManager = provider.GetRequiredService<JobManager>();

        Assert.False(await jobManager.StopJobs());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task StartJobs_AfterStopJobs_StartsAllJobsOnANewScheduler(int stopCount)
    {
        await using var provider = BuildServiceProvider();
        var jobManager = provider.GetRequiredService<JobManager>();
        await jobManager.StartJobs();
        var firstScheduler = await GetScheduler(provider);
        var expectedJobNames = await GetScheduledJobNames(firstScheduler);

        for (var i = 0; i < stopCount; i++)
        {
            Assert.True(await jobManager.StopJobs());
        }
        Assert.Equal(SchedulerStatus.Shutdown, firstScheduler.Status);

        await jobManager.StartJobs();

        var secondScheduler = await GetScheduler(provider);
        Assert.NotSame(firstScheduler, secondScheduler);
        Assert.Equal(SchedulerStatus.Running, secondScheduler.Status);
        Assert.Equal(expectedJobNames, await GetScheduledJobNames(secondScheduler));
        Assert.Equal(_now.AddSeconds(30), await jobManager.GetWeatherDataRefreshNextFireTimeAsync());
    }

    [Fact]
    public async Task StartJobs_RepeatedStopAndStart_KeepsWorking()
    {
        await using var provider = BuildServiceProvider();
        var jobManager = provider.GetRequiredService<JobManager>();

        for (var i = 0; i < 3; i++)
        {
            await jobManager.StartJobs();
            Assert.Equal(SchedulerStatus.Running, (await GetScheduler(provider)).Status);
            Assert.True(await jobManager.StopJobs());
        }
    }

    /// <summary>
    /// The replaced custom job factory created a dependency injection scope per job execution and disposed it afterward,
    /// so scoped services such as database contexts never outlive one execution.
    /// </summary>
    [Fact]
    public async Task AddJobScheduler_ResolvesEveryJobExecutionFromItsOwnScope()
    {
        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddJobScheduler()
            .AddSingleton<ExecutionRecorder>()
            .AddScoped<ScopedDependency>()
            .AddTransient<RecordingJob>()
            .BuildServiceProvider();
        var scheduler = await GetScheduler(provider);
        var job = JobBuilder.Create<RecordingJob>().WithIdentity(nameof(RecordingJob)).Build();
        var trigger = TriggerBuilder.Create()
            .StartNow()
            .WithSchedule(SimpleScheduleBuilder.Create().WithInterval(TimeSpan.FromMilliseconds(10)).WithRepeatCount(2))
            .Build();

        await scheduler.ScheduleJob(job, trigger);
        await scheduler.Start();

        var dependencies = await provider.GetRequiredService<ExecutionRecorder>().WaitForDisposedDependencies(3, TimeSpan.FromSeconds(10));
        Assert.Equal(3, dependencies.Distinct().Count());
        Assert.All(dependencies, d => Assert.True(d.IsDisposed));
    }

    private ServiceProvider BuildServiceProvider(bool shouldUseFakeSolarValues = false, bool restartNeeded = false, bool crashedOnStartup = false)
    {
        var configurationWrapper = new Mock<IConfigurationWrapper>();
        configurationWrapper.Setup(c => c.ChargingValueJobUpdateIntervall()).Returns(ChargingValueJobUpdateInterval);
        configurationWrapper.Setup(c => c.PvValueJobUpdateIntervall()).Returns(PvValueJobUpdateInterval);
        configurationWrapper.Setup(c => c.BleDataRefreshIntervalSeconds()).Returns(BleDataRefreshIntervalSeconds);
        configurationWrapper.Setup(c => c.CarRefreshAfterCommandSeconds()).Returns(CarRefreshAfterCommandSeconds);
        configurationWrapper.Setup(c => c.ShouldUseFakeSolarValues()).Returns(shouldUseFakeSolarValues);
        var settings = new Mock<ISettings>();
        settings.SetupGet(s => s.RestartNeeded).Returns(restartNeeded);
        settings.SetupGet(s => s.CrashedOnStartup).Returns(crashedOnStartup);
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.DateTimeOffSetNow()).Returns(_now);
        dateTimeProvider.Setup(d => d.DateTimeOffSetUtcNow()).Returns(_now);

        // The jobs themselves are not registered, so any trigger that fires while a test runs fails to build its job.
        return new ServiceCollection()
            .AddLogging()
            .AddJobScheduler()
            .AddSingleton(configurationWrapper.Object)
            .AddSingleton(settings.Object)
            .AddSingleton(dateTimeProvider.Object)
            .AddSingleton(Mock.Create<IConstants>())
            .AddSingleton<JobManager>()
            .BuildServiceProvider();
    }

    private static async Task<IScheduler> GetScheduler(IServiceProvider provider) =>
        await provider.GetRequiredKeyedService<ISchedulerFactory>(JobManager.SchedulerName).GetScheduler();

    private static async Task<List<string>> GetScheduledJobNames(IScheduler scheduler) =>
        (await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup())).Select(k => k.Name).Order().ToList();

    private sealed class ExecutionRecorder
    {
        private readonly ConcurrentQueue<ScopedDependency> _disposedDependencies = new();
        private readonly SemaphoreSlim _disposed = new(0);

        public void RecordDisposal(ScopedDependency dependency)
        {
            _disposedDependencies.Enqueue(dependency);
            _disposed.Release();
        }

        public async Task<List<ScopedDependency>> WaitForDisposedDependencies(int count, TimeSpan timeout)
        {
            for (var i = 0; i < count; i++)
            {
                Assert.True(await _disposed.WaitAsync(timeout), $"Only {i} of {count} job scopes were disposed.");
            }
            return _disposedDependencies.ToList();
        }
    }

    private sealed class ScopedDependency(ExecutionRecorder recorder) : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
            recorder.RecordDisposal(this);
        }
    }

    [DisallowConcurrentExecution]
    private sealed class RecordingJob(ScopedDependency dependency) : IJob
    {
        public ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
        {
            Assert.False(dependency.IsDisposed);
            return ValueTask.CompletedTask;
        }
    }
}
