namespace PkSoftwareService.Custom.Backend.Ble;

/// <summary>
/// Structured result classification of a BLE request. This is the wire contract between the BLE container and TSC:
/// the numeric values must never change, only new values may be appended. Presence decisions are made exclusively on
/// this enum, never on any message text.
/// </summary>
public enum BleCommandOutcome
{
    /// <summary>
    /// The command completed successfully.
    /// </summary>
    Ok = 0,
    /// <summary>
    /// The car's BLE beacon was not found within the scan budget. This is the only outcome that counts as evidence
    /// that the car is not at home.
    /// </summary>
    CarAbsent = 1,
    /// <summary>
    /// The beacon was seen but the connection, session or command failed afterwards. The car is present; the radio
    /// or the car had trouble. Must never be treated as "car not at home".
    /// </summary>
    LinkFailed = 2,
    /// <summary>
    /// The car is present and its body controller answers, but the infotainment system is unreachable because the
    /// car is asleep. TSC decides whether to wake it.
    /// </summary>
    CarAsleep = 3,
    /// <summary>
    /// The car executed the protocol but refused the command (e.g. charging-start while already charging). The
    /// refusal reason is in <see cref="DtoBleCommandResult.CarErrorMessage"/>.
    /// </summary>
    CarRefused = 4,
    /// <summary>
    /// The Bluetooth adapter exists but could not be used (HCI level failure). Local problem, no presence information.
    /// </summary>
    AdapterUnavailable = 5,
    /// <summary>
    /// The request was malformed. Indicates a bug, not an environment problem.
    /// </summary>
    InvalidRequest = 6,
    /// <summary>
    /// The BLE worker crashed or produced unparseable output. Local problem, no presence information.
    /// </summary>
    WorkerError = 7,
    /// <summary>
    /// The BLE worker did not answer in time and was killed. Local problem, no presence information.
    /// </summary>
    WorkerTimeout = 8,
    /// <summary>
    /// The adapter configured for this car is not present on the container's host. Explicit configuration error,
    /// never a silent fallback to a different radio.
    /// </summary>
    AdapterNotFound = 9,
}
