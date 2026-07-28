using TeslaSolarCharger.Server.Dtos.Ble;
using TeslaSolarCharger.Shared.Dtos.Ble;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IBleService
{
    Task<DtoBleCommandResult> StartCharging(string vin);
    Task<DtoBleCommandResult> StopCharging(string vin);
    Task<DtoBleCommandResult> SetAmp(string vin, int amps);
    Task<DtoBleCommandResult> FlashLights(string vin);
    Task<DtoBleCommandResult> PairKey(string vin, string role);
    Task<DtoBleCommandResult> WakeUpCar(string vin);
    Task CheckBleApiVersionCompatibilities();
    Task<DtoBleCommandResult> GetChargeState(string vin);
    Task<DtoBleCommandResult> GetDriveState(string vin);

    /// <summary>
    /// Gets the body controller state (sleep status, lock state, user presence) via the VCSEC domain. Works while the
    /// car is asleep and does not wake up the car. Fails with a beacon related error when the car is not in BLE range.
    /// </summary>
    Task<DtoBleCommandResult> GetBodyControllerState(string vin);

    /// <summary>
    /// Passively scans for the car's BLE advertisement without connecting, so it can never wake the car. The result
    /// message contains a <see cref="Server.Dtos.Ble.DtoBleBeaconScanResult"/> as JSON. Requires a BLE container of
    /// version 2.37.0 or later; older containers answer with a non success result.
    /// </summary>
    Task<DtoBleCommandResult> GetBeaconScanResult(string vin);
    Task<string?> CheckBleApiVersionCompatibility(string? host);

    /// <summary>
    /// Returns the distinct BLE containers (BLE API base URLs) configured on BLE enabled cars.
    /// </summary>
    List<DtoBleContainer> GetBleContainers();

    /// <summary>
    /// Downloads the in memory logs of the BLE container with the given base url.
    /// Returns null if the url is not configured on any car or the container is not reachable.
    /// </summary>
    Task<Stream?> DownloadLogs(string bleApiBaseUrl);
}
