using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PkSoftwareService.Custom.Backend;
using Serilog.Events;
using System.Text;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class DebugController(IFleetTelemetryConfigurationService fleetTelemetryConfigurationService,
    IDebugService debugService) : ApiBaseController
{
    [HttpGet]
    public IActionResult DownloadLogs()
    {
        var bytes = debugService.GetLogBytes();

        // Return the file with the appropriate content type and file name.
        return File(bytes, "text/plain", "logs.log");
    }

    [HttpGet]
    public IActionResult GetLogLevel()
    {
        var level = debugService.GetLogLevel();
        return Ok(new DtoValue<string>(level));
    }

    /// <summary>
    /// Adjusts the minimum log level for the in-memory sink.
    /// </summary>
    /// <param name="level">The new log level (e.g. Verbose, Debug, Information, Warning, Error, Fatal).</param>
    /// <returns>Status message.</returns>
    [HttpPost]
    public IActionResult SetLogLevel([FromQuery] string level)
    {
        debugService.SetLogLevel(level);
        return Ok();
    }

    [HttpGet]
    public IActionResult GetLogCapacity()
    {
        var capacity = debugService.GetLogCapacity();
        return Ok(new DtoValue<int>(capacity));
    }

    [HttpPost]
    public IActionResult SetLogCapacity([FromQuery] int capacity)
    {
        debugService.SetLogCapacity(capacity);
        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> GetFleetTelemetryConfiguration(string vin)
    {
        var config = await fleetTelemetryConfigurationService.GetFleetTelemetryConfiguration(vin);
        var configString = JsonConvert.SerializeObject(config, Formatting.Indented);
        return Ok(new DtoValue<string>(configString));
    }

    [HttpPost]
    public async Task<IActionResult> SetFleetTelemetryConfiguration(string vin, bool forceReconfiguration)
    {
        var config = await fleetTelemetryConfigurationService.SetFleetTelemetryConfiguration(vin, forceReconfiguration);
        var configString = JsonConvert.SerializeObject(config, Formatting.Indented);
        return Ok(new DtoValue<string>(configString));
    }

    [HttpGet]
    public async Task<IActionResult> GetCars()
    {
        var cars = await debugService.GetCars();
        return Ok(cars);
    }
}
