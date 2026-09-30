using TeslaSolarCharger.Server.Dtos.Ble;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IBleVehicleDataService
{
    /// <summary>
    /// Refreshes presence, sleep state and charge state via BLE for all cars whose data is collected via BLE.
    /// </summary>
    Task RefreshBleCarData();

    /// <summary>
    /// Refreshes presence, sleep state and charge state via BLE for a single car, if that car collects its data via
    /// BLE. Used when something needs an up to date state right away instead of waiting for the next scheduled run.
    /// Does nothing for cars that do not collect their data via BLE or when another read for the same car is already
    /// in progress.
    /// </summary>
    Task RefreshSingleCarData(int carId);

    /// <summary>
    /// Refreshes, one after the other, the configured groups that use one radio, from the presence answers already
    /// fetched for them. Part of the interface only so <see cref="RefreshBleCarData"/> can run every further radio on
    /// an instance from its own DI scope, and with it its own DbContext, in parallel. Never throws.
    /// </summary>
    Task RefreshRadioGroupSafely(List<DtoBleGroupPresence> groups);
}
