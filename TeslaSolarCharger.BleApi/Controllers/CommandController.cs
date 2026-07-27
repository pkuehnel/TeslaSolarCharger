using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.BleApi.Abstracts;
using TeslaSolarCharger.BleApi.Dtos;
using TeslaSolarCharger.BleApi.Services.Contracts;

namespace TeslaSolarCharger.BleApi.Controllers;

public class CommandController (ICommandService service) : ApiBaseController
{
    /// <summary>
    /// Send a command to the car via BLE
    /// </summary>
    /// <param name="vin">VIN of the car</param>
    /// <param name="command">command name of the car (e.g. charging-set-amps)</param>
    /// <param name="domain">add a domain, e.g. VCSEC</param>
    /// <param name="parameters">Array of parameters sent after the command, e.g. `6` to set current to 6 amps</param>
    /// <returns></returns>
    [HttpPost]
    public Task<DtoBleCommandResult> ExecuteCommand(string vin, string command, [FromBody] List<string> parameters, string? domain = null) => service.ExecuteCommand(vin, command, domain, parameters);

    /// <summary>
    /// Passively scan for the car's BLE advertisement without connecting (never wakes the car). The
    /// result message contains the scan outcome as JSON including how many advertisements of other
    /// devices were heard, so the caller can distinguish an absent car from a deaf Bluetooth radio.
    /// </summary>
    /// <param name="vin">VIN of the car</param>
    /// <returns></returns>
    [HttpGet]
    public Task<DtoBleCommandResult> BeaconScan(string vin) => service.BeaconScan(vin);

    /// <summary>
    /// Get a list of all available commands
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public Task<DtoBleCommandResult> ListCommands() => service.ListCommands();
}