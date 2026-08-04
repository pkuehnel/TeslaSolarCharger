using Microsoft.AspNetCore.Mvc;
using PkSoftwareService.Custom.Backend.Ble;
using TeslaSolarCharger.BleApi.Abstracts;
using TeslaSolarCharger.BleApi.Services.Contracts;

namespace TeslaSolarCharger.BleApi.Controllers;

public class CommandController (ICommandService service) : ApiBaseController
{
    /// <summary>
    /// Send a command to the car via BLE
    /// </summary>
    /// <param name="vin">VIN of the car</param>
    /// <param name="command">command name of the car (e.g. charging-set-amps)</param>
    /// <param name="parameters">Array of parameters sent after the command, e.g. `6` to set current to 6 amps</param>
    /// <param name="domain">accepted for interface stability; the worker decides per command whether VCSEC or infotainment is needed</param>
    /// <param name="adapter">optional stable adapter id (BD address); omitted = container default adapter</param>
    /// <param name="keepWarmSeconds">optional: keep the adapter's worker warm for this many seconds; requests without the parameter never change the stored window</param>
    /// <param name="useDebug">run the adapter's worker with debug logging; a worker started with a different setting is restarted first</param>
    /// <returns></returns>
    [HttpPost]
    public Task<DtoBleCommandResult> ExecuteCommand(string vin, string command, [FromBody] List<string> parameters,
        string? domain = null, string? adapter = null, int? keepWarmSeconds = null, bool useDebug = false)
        => service.ExecuteCommand(vin, command, domain, parameters, adapter, keepWarmSeconds, useDebug);

    /// <summary>
    /// Scan for the BLE advertisements of the given cars without connecting to any of them. All VINs share one scan
    /// window; a present car is typically heard within milliseconds.
    /// </summary>
    /// <param name="vins">VINs of the cars to scan for</param>
    /// <param name="adapter">optional stable adapter id (BD address); omitted = container default adapter</param>
    /// <param name="keepWarmSeconds">optional: keep the adapter's worker warm for this many seconds</param>
    /// <param name="useDebug">run the adapter's worker with debug logging; a worker started with a different setting is restarted first</param>
    [HttpPost]
    public Task<DtoBleBeaconScanResult> BeaconScan([FromBody] List<string> vins, string? adapter = null,
        int? keepWarmSeconds = null, bool useDebug = false, int? windowMs = null)
        => service.BeaconScan(vins, adapter, keepWarmSeconds, useDebug, windowMs);

    /// <summary>
    /// Get a list of all available commands
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public Task<DtoBleCommandResult> ListCommands() => service.ListCommands();
}
