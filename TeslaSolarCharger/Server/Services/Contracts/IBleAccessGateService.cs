using PkSoftwareService.Custom.Backend.Ble;

namespace TeslaSolarCharger.Server.Services.Contracts;

/// <summary>
/// Decides whether it is worth talking to a car over BLE at all right now. Two things make a request pointless
/// before it is sent, and both used to be found out the hard way once per poll interval on a radio every car shares:
/// a car that rejected TSC's key answers everything the same way, and adding a key stops the worker of that
/// container, so anything sent meanwhile can only time out. State is kept in memory only.
/// </summary>
public interface IBleAccessGateService
{
    /// <summary>
    /// Feeds one BLE answer back in. A rejected key starts (or restarts) the pause for that car; any successful
    /// request ends it, so the connection test after a pairing puts the car back into service immediately.
    /// </summary>
    void RegisterCommandResult(int carId, DtoBleCommandResult result);

    /// <summary>
    /// True while the car is known to reject TSC's key. Turns false by itself once the retry interval has passed, so
    /// a key added outside TSC is picked up without anyone having to tell TSC about it.
    /// </summary>
    bool IsKeyRejected(int carId);

    /// <summary>
    /// Forgets a car's key rejection, e.g. because the user asked for a connection test and expects it to be run
    /// rather than answered from memory.
    /// </summary>
    void ClearKeyRejection(int carId);

    /// <summary>
    /// Marks a container as busy adding a key until the returned scope is disposed. Pairing owns that container's
    /// Bluetooth adapter exclusively, so every scheduled read sent meanwhile fails with a connect timeout - on all
    /// cars of the container, not just the one being paired.
    /// </summary>
    IDisposable BeginPairing(string? bleApiBaseUrl);

    /// <summary>
    /// Whether a key is currently being added on the given container.
    /// </summary>
    bool IsPairingInProgress(string? bleApiBaseUrl);
}
