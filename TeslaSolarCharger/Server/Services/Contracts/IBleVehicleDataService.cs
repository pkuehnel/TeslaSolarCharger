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
}
