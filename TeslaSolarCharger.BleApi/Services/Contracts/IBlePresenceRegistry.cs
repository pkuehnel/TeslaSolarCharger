using PkSoftwareService.Custom.Backend.Ble;
using TeslaSolarCharger.BleApi.Dtos.Worker;

namespace TeslaSolarCharger.BleApi.Services.Contracts;

/// <summary>
/// Holds what the container knows about which cars are around, built from the worker's advertisement stream and from
/// the outcome of every command. State is in memory and per adapter, and it deliberately outlives the worker: a
/// worker restart must not look like every car leaving.
/// </summary>
public interface IBlePresenceRegistry
{
    /// <summary>Applies one advertisement digest of the background scan.</summary>
    void ApplyDigest(string adapterKey, WorkerResponse digest, DateTimeOffset at);

    /// <summary>
    /// Applies a scanner state change. This is how the container tells "heard nothing because the car is gone" from
    /// "heard nothing because the radio was busy or gone".
    /// </summary>
    void ApplyScanState(string adapterKey, string? scanState, string? reason, DateTimeOffset at);

    /// <summary>
    /// Records the outcome of a command. Ok, a refusal and "asleep" all prove the car answered, which is the only
    /// presence evidence available while we hold a connection - the car emits no advertisements then.
    /// </summary>
    void NoteCommandOutcome(string adapterKey, string vin, BleCommandOutcome? outcome, DateTimeOffset at);

    /// <summary>
    /// Whether the car was heard within <paramref name="maxAge"/>. Returns true while the scan has not been
    /// observing that long yet: not heard yet is not the same as not there.
    /// </summary>
    bool WasHeardWithin(string adapterKey, string vin, TimeSpan maxAge, DateTimeOffset now);

    /// <summary>
    /// Whether the adapter is scanning but has received nothing at all for longer than the threshold. The observed
    /// "adapter is up but hears nothing" failure, which only a fresh adapter bind recovers from.
    /// </summary>
    bool IsDeaf(string adapterKey, TimeSpan silenceThreshold, DateTimeOffset now);

    /// <summary>Marks the adapter as no longer observed, e.g. because its worker stopped.</summary>
    void ForgetAdapter(string adapterKey);

    DtoBlePresenceResult GetPresence(string adapterKey, IReadOnlyList<string> vins, TimeSpan maxAge, DateTimeOffset now);
}
