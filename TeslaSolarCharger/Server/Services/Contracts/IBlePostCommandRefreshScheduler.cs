namespace TeslaSolarCharger.Server.Services.Contracts;

/// <summary>
/// Schedules an extra BLE data read shortly after a successful charge command so the changed values (e.g. the new
/// charging amps) show up quickly instead of only on the next charging cycle.
/// </summary>
public interface IBlePostCommandRefreshScheduler
{
    /// <summary>
    /// Schedules a one off BLE read for the given car after the configured delay
    /// (<see cref="TeslaSolarCharger.Shared.Contracts.IConfigurationWrapper.BleDataRefreshAfterCommandSeconds"/>).
    /// Multiple calls for the same car within the delay window coalesce into a single read timed from the latest call.
    /// </summary>
    Task ScheduleRefresh(int carId);
}
