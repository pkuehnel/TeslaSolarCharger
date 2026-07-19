namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IBleVehicleDataService
{
    /// <summary>
    /// Refreshes presence, sleep state and charge state via BLE for all cars whose data is collected via BLE.
    /// </summary>
    Task RefreshBleCarData();
}
