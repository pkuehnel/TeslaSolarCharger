using PkSoftwareService.Custom.Backend.Ble;
using TeslaSolarCharger.BleApi.Dtos;

namespace TeslaSolarCharger.BleApi.Services.Contracts;

public interface IBleWorkerService
{
    Task<DtoBleCommandResult> ExecuteCommand(string? adapter, string vin, string command, List<string> parameters, int? keepWarmSeconds);
    Task<DtoBleBeaconScanResult> BeaconScan(string? adapter, List<string> vins, int? keepWarmSeconds);
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

    List<DtoBleWorkerStatus> GetStatuses();
    List<DtoBleWorkerEvent> GetEvents(string? adapter, int? tail);
    /// <summary>
    /// Canonical keys of adapters whose worker currently runs (holds the exclusive user channel).
    /// </summary>
    IReadOnlyCollection<string> GetRunningAdapterKeys();
}
