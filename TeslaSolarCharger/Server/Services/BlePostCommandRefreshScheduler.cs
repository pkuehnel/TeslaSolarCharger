using Quartz;
using TeslaSolarCharger.Server.Scheduling.Jobs;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class BlePostCommandRefreshScheduler(
    ILogger<BlePostCommandRefreshScheduler> logger,
    ISchedulerFactory schedulerFactory,
    IConfigurationWrapper configurationWrapper) : IBlePostCommandRefreshScheduler
{
    internal const string CarIdJobDataKey = "carId";

    public async Task ScheduleRefresh(int carId)
    {
        logger.LogTrace("{method}({carId})", nameof(ScheduleRefresh), carId);
        var delaySeconds = configurationWrapper.BleDataRefreshAfterCommandSeconds();
        IScheduler scheduler;
        try
        {
            scheduler = await schedulerFactory.GetScheduler().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not get scheduler to schedule delayed BLE refresh for car {carId}", carId);
            return;
        }

        try
        {
            //Ensure the durable job exists. It is normally added in JobManager.StartJobs, this is a defensive fallback.
            var jobKey = new JobKey(nameof(BlePostCommandRefreshJob));
            if (!await scheduler.CheckExists(jobKey).ConfigureAwait(false))
            {
                var job = JobBuilder.Create<BlePostCommandRefreshJob>()
                    .WithIdentity(jobKey)
                    .StoreDurably()
                    .Build();
                await scheduler.AddJob(job, true).ConfigureAwait(false);
            }

            //Stable per car trigger key: if a change happened recently and a refresh is still pending, rescheduling the
            //same key coalesces the changes into a single read timed from the latest change.
            var triggerKey = new TriggerKey($"blePostCommandRefresh-{carId}");
            var trigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey)
                .ForJob(jobKey)
                .UsingJobData(CarIdJobDataKey, carId)
                .StartAt(DateTimeOffset.UtcNow.AddSeconds(delaySeconds))
                .Build();

            if (await scheduler.CheckExists(triggerKey).ConfigureAwait(false))
            {
                await scheduler.RescheduleJob(triggerKey, trigger).ConfigureAwait(false);
            }
            else
            {
                await scheduler.ScheduleJob(trigger).ConfigureAwait(false);
            }
            logger.LogDebug("Scheduled delayed BLE refresh for car {carId} in {delaySeconds} s", carId, delaySeconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not schedule delayed BLE refresh for car {carId}", carId);
        }
    }
}
