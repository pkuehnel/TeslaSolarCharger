using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Dtos.Ble;

/// <summary>
/// Snapshot of the BLE sleep window state of a car, used for the UI.
/// </summary>
public class DtoBleSleepWindowStatus
{
    public BleSleepPhase Phase { get; set; }

    /// <summary>
    /// Seconds until the next transition: while <see cref="BleSleepPhase.TryingToSleep"/> the remaining window time
    /// until the next infotainment poll; while <see cref="BleSleepPhase.WaitingToSleep"/> the (best effort) remaining
    /// stability time until a window could start. Never negative. Null while <see cref="BleSleepPhase.Asleep"/>.
    /// </summary>
    public int? SecondsRemaining { get; set; }
}
