using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.Contracts;

/// <summary>
/// Tracks, per BLE data collection car, how many consecutive BLE reads reported the car as out of BLE range. A single
/// out of range result can also be caused by a transient BLE stack failure while the car is at home, so the car is
/// only confirmed as away after multiple consecutive out of range results. Also tracks per BLE container/adapter when
/// the radio last provably received anything, as the only available evidence against a dead radio. State is kept in
/// memory only.
/// </summary>
public interface IBlePresenceStateService
{
    /// <summary>
    /// Resets the consecutive out of range counter of a car because a BLE read succeeded, proving the car is in range.
    /// </summary>
    void RegisterSuccessfulRead(int carId);

    /// <summary>
    /// Registers an out of BLE range poll result. Returns <see cref="BleAwayConfirmation.JustConfirmed"/> exactly once,
    /// on the poll that reaches the required number of consecutive out of range results, so the caller can run the
    /// away transition exactly once.
    /// </summary>
    BleAwayConfirmation RegisterOutOfRange(int carId);

    /// <summary>
    /// True while out of range results were registered but the car is not yet confirmed as away. During this window
    /// the last known car state stays valid but no new charging commands should be sent to the car.
    /// </summary>
    bool IsPresenceUncertain(int carId);

    /// <summary>
    /// Clears the presence state of a car entirely.
    /// </summary>
    void Reset(int carId);

    /// <summary>
    /// Drops the presence state of every car not contained in <paramref name="carIds"/>. Called with the currently BLE
    /// polled cars each refresh cycle so a car that left BLE data collection mode (or a disabled global switch) cannot
    /// keep a stale uncertain state that would suppress its charging commands forever.
    /// </summary>
    void RetainOnly(IReadOnlyCollection<int> carIds);

    /// <summary>
    /// Registers the result of a beacon scan for radio silence tracking: heardAnything is true when the scan received
    /// any advertisement at all (a car's or another device's). Returns how long the radio has provably received
    /// nothing, measured from the first registration if it never heard anything.
    /// </summary>
    TimeSpan RegisterScanEvidence(string containerKey, bool heardAnything, DateTimeOffset timestamp);
}
