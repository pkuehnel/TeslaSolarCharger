using TeslaSolarCharger.Shared.Dtos.Ble;

namespace TeslaSolarCharger.Server.Services.Contracts;

/// <summary>
/// In memory diagnostics of the BLE presence: per BLE container/adapter when the radio last provably received
/// anything, as the only available evidence against a dead radio, and the recent presence observations of each car.
/// </summary>
public interface IBlePresenceStateService
{
    /// <summary>
    /// Records a failed read of a present car and returns whether it counts as a data collection error: only when the
    /// car was heard (advertisement or answered command) after its previous failed read, or when there was none.
    ///
    /// A car that just drove away stays present until its last sighting passes the max age, and every read in that
    /// time fails. It is never heard again, so only the first of those failures counts and the error never reaches its
    /// threshold. A car at home with a bad link keeps being heard, so all of its failures count.
    /// </summary>
    /// <param name="lastSeenAt">When the car was last heard according to the presence answer the read was based on,
    /// null when unknown - the failure then always counts.</param>
    bool RegisterFailedRead(int carId, DateTime? lastSeenAt, DateTime failedAt);

    /// <summary>
    /// Drops the observations and failed reads of every car not contained in <paramref name="carIds"/>. Called with the currently
    /// BLE polled cars each refresh cycle so a car that left BLE data collection does not keep its history forever.
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
