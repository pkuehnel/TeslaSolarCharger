namespace PkSoftwareService.Custom.Backend.Ble;

/// <summary>
/// The phase of a BLE request in which the final result was decided. Diagnostic only; presence decisions are made on
/// <see cref="BleCommandOutcome"/> alone.
/// </summary>
public enum BleCommandPhase
{
    Scan = 0,
    Connect = 1,
    Session = 2,
    Command = 3,
}
