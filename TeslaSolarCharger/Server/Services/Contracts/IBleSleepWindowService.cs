using TeslaSolarCharger.Server.Dtos.Ble;
using VehicleStatus = VCSEC.VehicleStatus;

namespace TeslaSolarCharger.Server.Services.Contracts;

/// <summary>
/// Tracks, per car, whether an idle BLE car is currently in a "sleep window" during which the infotainment charge
/// state is not polled so the car's standby timer can run out and it can fall asleep. VCSEC (body controller state)
/// polling continues throughout. State is kept in memory only.
/// </summary>
public interface IBleSleepWindowService
{
    /// <summary>
    /// Decides whether the infotainment charge state should be polled this cycle for an awake, non charging BLE car.
    /// Returns false while the car is inside an active sleep window. A window that has reached its full duration is
    /// ended here and true is returned so a single fresh poll happens. Returns true when the feature is disabled
    /// (<paramref name="windowMinutes"/> &lt;= 0) or when the car is not (yet) in a window.
    /// </summary>
    bool ShouldPollInfotainment(int carId, DateTime nowUtc, int windowMinutes);

    /// <summary>
    /// Records the observed signals of a successful full poll (VCSEC body controller state plus infotainment charge
    /// state) and starts or re-starts a sleep window once the car has been unchanged, with all counted closures closed
    /// and no occupant, for the stability period. Must only be called after a full poll actually happened.
    /// </summary>
    void ObserveFullPoll(int carId, VehicleStatus bodyControllerState, bool? pluggedIn, int? chargeLimitSoc,
        DateTime nowUtc, int windowMinutes, int stabilityMinutes);

    /// <summary>
    /// Marks a tracked car as asleep (the sleep window succeeded). The state is kept so the UI can show the asleep
    /// phase; the next awake full poll restarts the stability period as if the car had just arrived.
    /// </summary>
    void NotifyAsleep(int carId);

    /// <summary>
    /// Clears the sleep window state of a car entirely. Called when the car left home, is charging, a charge command
    /// was sent or the user cancelled the sleep attempt. The next awake full poll restarts the stability period as if
    /// the car had just arrived.
    /// </summary>
    void ResetSleepWindow(int carId);

    /// <summary>
    /// Current sleep window status of a car for the UI, or null if the car is not currently tracked or the feature is
    /// disabled.
    /// </summary>
    DtoBleSleepWindowStatus? GetStatus(int carId, DateTime nowUtc, int windowMinutes, int stabilityMinutes);

    /// <summary>
    /// Starts a sleep window right away, skipping the remaining stability period, because the user explicitly asked
    /// for it. Returns false if that is currently not possible: feature disabled, car not tracked, already asleep,
    /// already in a window, or the last full poll saw an open closure or an occupant.
    /// </summary>
    bool TryStartWindowNow(int carId, DateTime nowUtc, int windowMinutes);
}
