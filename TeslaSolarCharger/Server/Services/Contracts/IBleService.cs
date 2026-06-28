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
