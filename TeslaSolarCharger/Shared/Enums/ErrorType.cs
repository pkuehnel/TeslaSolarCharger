namespace TeslaSolarCharger.Shared.Enums;

public enum ErrorType
{
    /// <summary>
    /// The car could not execute the command. E.g. could not start charging because is already charging.
    /// </summary>
    CarExecution,
    /// <summary>
    /// The command could not be executed due to an error when calling TeslaControl, E.G. BLE errors, car not found,...
    /// </summary>
    TeslaControl,
    /// <summary>
    /// The BLE API container is not correctly configured, e.g. private key is missing.
    /// </summary>
    BleApiConfiguration,
    /// <summary>
    /// An unknown error occured while executing the command.
    /// </summary>
    Exceptional,
    /// <summary>
    /// TSC is not configured correctly to handle BLE commands
    /// </summary>
    TscConfiguration = 100,
    /// <summary>
    /// The reason for the error is unknown
    /// </summary>
    Unknown = 1000,
}
