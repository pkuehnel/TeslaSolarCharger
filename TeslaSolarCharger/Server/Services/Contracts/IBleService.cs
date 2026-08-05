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
    /// Asks one container what it knows about the given cars: how long ago each was last heard, by advertisement or
    /// by a command it answered. Answered from the container's memory, so it never touches the radio, never wakes a
    /// car, and costs nothing at all for a car that is not there.
    /// </summary>
    /// <param name="maxAgeSeconds">
    /// How old the newest evidence may be and still count as present. Null leaves the container's own default.
    /// </param>
    Task<DtoBlePresenceResult> GetPresence(string? host, string? adapter, List<string> vins,
        int? keepWarmSeconds, int? maxAgeSeconds = null);

    /// <summary>
    /// Presence of a single car on the container and adapter configured for that car. Never sends keepWarm, so a
    /// manual check does not change the container's warm window.
    /// </summary>
    Task<DtoBlePresenceResult> GetPresenceForVin(string vin);

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
