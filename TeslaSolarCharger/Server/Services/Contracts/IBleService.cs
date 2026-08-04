using PkSoftwareService.Custom.Backend.Ble;
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
    /// car is asleep and does not wake up the car. Fails with <see cref="BleCommandOutcome.CarAbsent"/> when the car
    /// is not in BLE range.
    /// </summary>
    Task<DtoBleCommandResult> GetBodyControllerState(string vin);

    /// <summary>
    /// Scans for the BLE advertisements of the given cars on one container and adapter without connecting to any of
    /// them, so no car is woken. All VINs share one scan window, which is why an absent car costs the window once
    /// per group instead of a full connect timeout per car.
    /// </summary>
    /// <param name="windowSeconds">
    /// How long the container listens before giving up. Null leaves the container's own default in place. The scan
    /// ends as soon as every car was heard, so a longer window only costs time when a car really is away.
    /// </param>
    Task<DtoBleBeaconScanResult> GetBeaconScanResults(string? host, string? adapter, List<string> vins,
        int? keepWarmSeconds, int? windowSeconds = null);

    /// <summary>
    /// Beacon scan for a single car on the container and adapter configured for that car. Never sends keepWarm, so a
    /// manual check does not change the container's warm window.
    /// </summary>
    Task<DtoBleBeaconScanResult> GetBeaconScanResultForVin(string vin);

    /// <summary>
    /// Lists the Bluetooth adapters of the container with the given base url, so a car can be pinned to a specific
    /// adapter (e.g. a USB dongle with its own antenna).
    /// </summary>
    Task<List<DtoBleAdapter>> GetAdapters(string? host);
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
