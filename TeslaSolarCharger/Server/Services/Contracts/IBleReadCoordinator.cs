namespace TeslaSolarCharger.Server.Services.Contracts;

/// <summary>
/// Coordinates BLE data reads so a single car is never read via BLE from two places at once (e.g. the scheduled
/// refresh job and a single car read triggered from the UI hitting the same BLE container). Registered as a singleton
/// as <see cref="IBleVehicleDataService"/> is transient and therefore can not hold shared state itself.
/// </summary>
public interface IBleReadCoordinator
{
    /// <summary>
    /// Tries to mark the car as currently being read. Returns true if the caller acquired the read slot and must call
    /// <see cref="EndRead"/> when done. Returns false if another read for this car is already in progress.
    /// </summary>
    bool TryBeginRead(int carId);

    /// <summary>
    /// Releases the read slot previously acquired via <see cref="TryBeginRead"/>.
    /// </summary>
    void EndRead(int carId);
}
