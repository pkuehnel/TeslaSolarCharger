using TeslaSolarCharger.BleApi.Dtos;

namespace TeslaSolarCharger.BleApi.Services.Contracts;

/// <summary>
/// Talks to the long living tesla-bled worker process. The worker keeps the Bluetooth adapter open, so commands no
/// longer pay for an adapter reset each time. It is started on the first request and stopped again after an idle
/// period, which frees the adapter for other uses (e.g. pairing).
/// </summary>
public interface IBleDaemonService
{
    /// <summary>
    /// Executes a command on a car. Starts the worker if needed and restarts it when the debug setting changed.
    /// </summary>
    Task<DtoBleCommandResult> ExecuteCommand(string vin, string command, List<string> parameters, bool useDebug);

    /// <summary>
    /// Checks whether the car currently advertises, without connecting to it.
    /// </summary>
    Task<DtoBleCommandResult> BeaconScan(string vin, bool useDebug);

    /// <summary>
    /// Stops the worker so another process can use the Bluetooth adapter. The caller must hold the adapter gate.
    /// </summary>
    Task StopWorker();

    DtoBleDaemonStatus GetStatus();

    List<DtoBleSessionEvent> GetEvents(int? tail);
}
