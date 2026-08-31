using TeslaSolarCharger.Shared.Dtos.Ble;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.Contracts;

/// <summary>
/// Turns "how long ago was this car last heard" into a presence decision. The BLE container answers that question
/// from its permanent background scan and from every command a car answered; a Tesla emits nothing at all while it
/// holds a connection to us, so those two sources are complementary and only both fall silent when the car is gone.
/// Also tracks per BLE container/adapter when the radio last provably received anything, as the only available
/// evidence against a dead radio. State is kept in memory only.
/// </summary>
public interface IBlePresenceStateService
{
    /// <summary>
    /// Registers the age of the newest evidence about a car. <paramref name="age"/> is null when nothing can be
    /// concluded (the container's scan is still warming up, or not running), which keeps the last known state and
    /// records no miss. Returns <see cref="BlePresenceDecision.JustConfirmedAway"/> exactly once, so the caller runs
    /// the away transition a single time.
    /// </summary>
    BlePresenceDecision RegisterPresenceAge(int carId, TimeSpan? age, TimeSpan maxAge);

    /// <summary>
    /// True while the car has not been heard for longer than the max age but is not confirmed away yet. During this
    /// window the last known car state stays valid but no new charging commands should be sent.
    /// </summary>
    bool IsPresenceUncertain(int carId);

    /// <summary>
    /// Clears the presence state of a car entirely.
    /// </summary>
    void Reset(int carId);

    /// <summary>
    /// Drops the presence state of every car not contained in <paramref name="carIds"/>. Called with the currently
    /// BLE polled cars each refresh cycle so a car that left BLE data collection mode (or a disabled global switch)
    /// cannot keep a stale uncertain state that would suppress its charging commands forever.
    /// </summary>
    void RetainOnly(IReadOnlyCollection<int> carIds);

    /// <summary>
    /// Registers whether the container's radio provably received anything at all. Returns how long it has heard
    /// nothing, measured from the first registration if it never heard anything.
    /// </summary>
    TimeSpan RegisterRadioEvidence(string containerKey, bool heardAnything, DateTimeOffset timestamp);

    /// <summary>
    /// Records what was known about a car at one poll, whether or not it was present. Purely diagnostic: this never
    /// influences presence, it exists so an unreliable link can be looked at instead of guessed about.
    /// </summary>
    void RegisterObservation(int carId, DtoBleBeaconObservation observation);

    /// <summary>
    /// The recorded observations of a car, oldest first, plus summary figures. Empty when nothing was recorded yet.
    /// </summary>
    DtoBleBeaconHistory GetObservations(int carId);
}
