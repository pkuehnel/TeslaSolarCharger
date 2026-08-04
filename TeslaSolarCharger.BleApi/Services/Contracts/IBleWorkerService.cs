using PkSoftwareService.Custom.Backend.Ble;
using TeslaSolarCharger.BleApi.Dtos;

namespace TeslaSolarCharger.BleApi.Services.Contracts;

public interface IBleWorkerService
{
    /// <summary>
    /// Executes a command on the worker of the target adapter. <paramref name="useDebug"/> is the setting TSC sent;
    /// a worker that runs with a different setting is restarted first, as the log level of the used library is global
    /// per process. Workers on other adapters keep serving with their own setting until their next request.
    /// </summary>
    Task<DtoBleCommandResult> ExecuteCommand(string? adapter, string vin, string command, List<string> parameters,
        int? keepWarmSeconds, bool useDebug);
    Task<DtoBleBeaconScanResult> BeaconScan(string? adapter, List<string> vins, int? keepWarmSeconds, bool useDebug,
        int? windowMs = null);
    /// <summary>
    /// State of the adapter's permanent background beacon scan, including per car how long ago it was heard. Answered
    /// from the worker's memory without touching the radio. Starts the worker if it is not running, because a stopped
    /// worker means no scan is happening at all.
    /// </summary>
    Task<DtoBleScannerStatus> ScannerStatus(string? adapter, List<string> vins, int? keepWarmSeconds, int? maxAgeMs);
    /// <summary>
    /// Stops the worker owning the target adapter, runs the action while holding the adapter exclusively (the action
    /// receives the current hciX id, empty for "first available") and lets the worker restart lazily afterwards.
    /// Used by pairing, which still shells out to tesla-control.
    /// </summary>
    Task<DtoBleCommandResult> RunWithExclusiveAdapter(string? adapter, Func<string, Task<DtoBleCommandResult>> action);
    /// <summary>
    /// Round trip liveness check of the worker owning the adapter. Unlike the process state in
    /// <see cref="GetStatuses"/> this proves the worker's request loop still answers, which a hung worker does not.
    /// Never starts a worker: a probe must not have side effects, so a stopped worker is reported as such.
    /// </summary>
    Task<DtoBleCommandResult> PingWorker(string? adapter);

    /// <summary>
    /// Stops the worker of the adapter (all workers when no adapter is given) so the next request starts it with the
    /// current flags. Used to apply changed scanner overrides; a stop is safe at any time because every worker is
    /// started lazily.
    /// </summary>
    Task RestartWorkers(string? adapter, string reason);

    List<DtoBleWorkerStatus> GetStatuses();
    List<DtoBleWorkerEvent> GetEvents(string? adapter, int? tail);
    /// <summary>
    /// Canonical keys of adapters whose worker currently runs (holds the exclusive user channel).
    /// </summary>
    IReadOnlyCollection<string> GetRunningAdapterKeys();
}
