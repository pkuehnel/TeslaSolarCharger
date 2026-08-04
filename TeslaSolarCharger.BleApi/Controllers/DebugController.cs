using Microsoft.AspNetCore.Mvc;
using PkSoftwareService.Custom.Backend;
using PkSoftwareService.Custom.Backend.Ble;
using Serilog.Core;
using Serilog.Events;
using System.Text;
using TeslaSolarCharger.BleApi.Abstracts;
using TeslaSolarCharger.BleApi.Dtos;
using TeslaSolarCharger.BleApi.InMemoryValues.Contracts;
using TeslaSolarCharger.BleApi.Services.Contracts;

namespace TeslaSolarCharger.BleApi.Controllers;

public class DebugController(IInMemorySink inMemorySink,
    LoggingLevelSwitch inMemoryLogLevelSwitch,
    IBleWorkerService bleWorkerService,
    ISettings settings) : ApiBaseController
{
    /// <summary>
    /// State of the adapter's permanent background beacon scan: per car how long ago it was heard, how it was
    /// recognized and how often it advertises, plus the counters that tell a working radio from a deaf one. Answered
    /// from the worker's memory without touching the radio, and it starts the worker when none is running.
    /// </summary>
    /// <param name="vins">Comma separated VINs to report on. Cars the radio heard are listed either way.</param>
    /// <param name="adapter">Optional stable adapter id (BD address); omitted = container default adapter.</param>
    /// <param name="keepWarmSeconds">Keeps the worker (and with it the scan) alive between polls.</param>
    /// <param name="maxAgeSeconds">Overrides how long ago a car may last have been heard and still count as present.</param>
    [HttpGet]
    public Task<DtoBleScannerStatus> ScannerStatus(string? vins = null, string? adapter = null,
        int? keepWarmSeconds = 600, int? maxAgeSeconds = null)
    {
        var vinList = (vins ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        return bleWorkerService.ScannerStatus(adapter, vinList, keepWarmSeconds, maxAgeSeconds * 1000);
    }

    /// <summary>
    /// Overrides the background scan flags of the worker and stops it, so the next request starts it with them. Exists
    /// to compare the scan modes on real hardware in one sitting; nothing is persisted, a container restart returns to
    /// the configured values. Parameters that are not given keep their current override.
    /// </summary>
    /// <param name="reset">Drops every override first, so a single call can return to the configured behaviour.</param>
    [HttpPost]
    public async Task<DtoBleScannerOverrides> SetScannerMode(bool? presenceScanEnabled = null,
        bool? scanWhileConnected = null, int? presenceMaxAgeSeconds = null, int? scanRestartAfterSeconds = null,
        int? addressBindingTtlSeconds = null, bool reset = false, string? adapter = null)
    {
        var overrides = settings.ScannerOverrides;
        if (reset)
        {
            overrides.PresenceScanEnabled = null;
            overrides.ScanWhileConnected = null;
            overrides.PresenceMaxAgeSeconds = null;
            overrides.ScanRestartAfterSeconds = null;
            overrides.AddressBindingTtlSeconds = null;
        }
        overrides.PresenceScanEnabled = presenceScanEnabled ?? overrides.PresenceScanEnabled;
        overrides.ScanWhileConnected = scanWhileConnected ?? overrides.ScanWhileConnected;
        overrides.PresenceMaxAgeSeconds = presenceMaxAgeSeconds ?? overrides.PresenceMaxAgeSeconds;
        overrides.ScanRestartAfterSeconds = scanRestartAfterSeconds ?? overrides.ScanRestartAfterSeconds;
        overrides.AddressBindingTtlSeconds = addressBindingTtlSeconds ?? overrides.AddressBindingTtlSeconds;
        await bleWorkerService.RestartWorkers(adapter, "scanner mode changed").ConfigureAwait(false);
        return overrides;
    }

    /// <summary>
    /// Status of every per-adapter BLE worker (state, uptime, keep warm window, request and outcome counters).
    /// </summary>
    [HttpGet]
    public List<DtoBleWorkerStatus> WorkerStatus() => bleWorkerService.GetStatuses();

    /// <summary>
    /// Recent BLE worker lifecycle and protocol events.
    /// </summary>
    /// <param name="adapter">Optional adapter key to filter by.</param>
    /// <param name="tail">Optional number of latest events to return.</param>
    [HttpGet]
    public List<DtoBleWorkerEvent> WorkerEvents(string? adapter = null, int? tail = null) => bleWorkerService.GetEvents(adapter, tail);

    /// <summary>
    /// Round trip liveness check of a running BLE worker. Proves the worker still answers, which the process state
    /// alone does not. Does not start a worker.
    /// </summary>
    /// <param name="adapter">Optional stable adapter id (BD address); omitted = container default adapter.</param>
    [HttpGet]
    public Task<DtoBleCommandResult> PingWorker(string? adapter = null) => bleWorkerService.PingWorker(adapter);

    /// <summary>
    /// Gets the current in memory logs.
    /// </summary>
    /// <param name="tail">Optional number of latest log entries to return.</param>
    /// <returns>List of log entries.</returns>
    [HttpGet]
    public ActionResult<List<string>> GetLogs(int? tail)
    {
        return Ok(inMemorySink.GetLogs(tail));
    }

    /// <summary>
    /// Downloads the in memory logs as a text file.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> DownloadInMemoryLogs()
    {
        var stream = new MemoryStream();
        // leaveOpen so the stream is not closed before it is returned to the client.
        await using (var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 4096, leaveOpen: true))
        {
            await inMemorySink.StreamLogsAsync(writer);
            await writer.FlushAsync();
        }
        stream.Position = 0; // Reset position to beginning

        return File(stream, "text/plain", "ble-logs.log");
    }

    [HttpGet]
    public IActionResult GetInMemoryLogLevel()
    {
        return Ok(new DtoValue<string>(inMemoryLogLevelSwitch.MinimumLevel.ToString()));
    }

    /// <summary>
    /// Adjusts the minimum log level for the in-memory sink.
    /// </summary>
    /// <param name="level">The new log level (e.g. Verbose, Debug, Information, Warning, Error, Fatal).</param>
    [HttpPost]
    public IActionResult SetInMemoryLogLevel([FromQuery] string level)
    {
        if (!Enum.TryParse<LogEventLevel>(level, true, out var newLevel))
        {
            return BadRequest("Invalid log level. Use one of: Verbose, Debug, Information, Warning, Error, Fatal");
        }
        inMemoryLogLevelSwitch.MinimumLevel = newLevel;
        return Ok();
    }

    [HttpGet]
    public IActionResult GetInMemoryLogCapacity()
    {
        return Ok(new DtoValue<int>(inMemorySink.GetCapacity()));
    }

    [HttpPost]
    public IActionResult SetInMemoryLogCapacity([FromQuery] int capacity)
    {
        try
        {
            inMemorySink.UpdateCapacity(capacity);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        return Ok();
    }
}
