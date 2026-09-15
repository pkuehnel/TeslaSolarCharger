using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services;

public class DeferredChargingTestObserver(
    ILogger<DeferredChargingTestObserver> logger,
    ISettings settings,
    IDeferredSetupCheckService deferredSetupCheckService,
    IDateTimeProvider dateTimeProvider)
    : IDeferredChargingTestObserver
{
    /// <summary>
    /// How old a reading may be and still count as "right now". A car that reported charging yesterday is not proof
    /// that charging works today.
    /// </summary>
    private static readonly TimeSpan MaximumReadingAge = TimeSpan.FromMinutes(15);

    public async Task ObserveRunningCharges(CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.DateTimeOffSetUtcNow();

        //Worked out from memory first. This runs inside the charging loop, and when nothing is charging - which is
        //most of the time - there is nothing a postponed test could be settled by, so nothing is read from storage.
        var chargingCarIds = settings.Cars
            .Where(c => IsFresh(c.IsCharging.Timestamp, now) && c.IsCharging.Value == true
                        && IsFresh(c.IsHomeGeofence.Timestamp, now) && c.IsHomeGeofence.Value == true)
            .Select(c => c.Id)
            .ToHashSet();
        var chargingConnectorIds = settings.OcppConnectorStates
            .Where(s => IsFresh(s.Value.IsCharging.Timestamp, now) && s.Value.IsCharging.Value)
            .Select(s => s.Key)
            .ToHashSet();

        if (chargingCarIds.Count == 0 && chargingConnectorIds.Count == 0)
        {
            return;
        }

        List<Shared.Dtos.Setup.DtoDeferredSetupCheck> outstandingChecks;
        try
        {
            outstandingChecks = (await deferredSetupCheckService.GetDeferredChecks().ConfigureAwait(false))
                .Where(c => c.Kind == DeferredSetupCheckKind.RealChargingTest
                            && c.State != SetupCheckResultState.Succeeded
                            && c.DeviceId != null)
                .ToList();
        }
        catch (Exception exception)
        {
            //Nothing here may disturb charging, which is the job this runs inside.
            logger.LogWarning(exception, "Could not read the postponed charging tests.");
            return;
        }

        foreach (var check in outstandingChecks)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var isCharging = check.DeviceKind switch
            {
                //A car charging away from home says nothing about whether this installation can control it, so
                //being at home is part of the proof. A connector only exists where it is installed.
                SetupDeviceKind.Car => chargingCarIds.Contains(check.DeviceId!.Value),
                SetupDeviceKind.ChargingStationConnector => chargingConnectorIds.Contains(check.DeviceId!.Value),
                _ => false,
            };
            if (!isCharging)
            {
                continue;
            }

            logger.LogInformation("{deviceKind} {deviceId} is charging, so its postponed charging test is settled.",
                check.DeviceKind, check.DeviceId);
            await deferredSetupCheckService
                .RecordCheckResult(check.Id, SetupCheckResultState.Succeeded, null)
                .ConfigureAwait(false);
        }
    }

    private static bool IsFresh(DateTimeOffset timestamp, DateTimeOffset now) => now - timestamp <= MaximumReadingAge;
}
