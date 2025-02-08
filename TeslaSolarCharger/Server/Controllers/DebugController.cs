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

public class DebugController(InMemorySink inMemorySink,
    Serilog.Core.LoggingLevelSwitch inMemoryLogLevelSwitch,
    IFleetTelemetryConfigurationService fleetTelemetryConfigurationService,/*chaning log level switch is not tested*/
    ISettings settings,
    IDebugService debugService) : ApiBaseController
{
    [HttpGet]
    public IActionResult DownloadLogs()
    {
        // Get the logs from the in-memory sink.
        var logs = inMemorySink.GetLogs();

        // Join the log entries into a single string, separated by new lines.
        var content = string.Join(Environment.NewLine, logs);

        // Convert the string content to a byte array (UTF8 encoding).
        var bytes = Encoding.UTF8.GetBytes(content);

        // Return the file with the appropriate content type and file name.
        return File(bytes, "text/plain", "logs.log");
    }

    /// <summary>
    /// Adjusts the minimum log level for the in-memory sink.
    /// </summary>
    /// <param name="level">The new log level (e.g. Verbose, Debug, Information, Warning, Error, Fatal).</param>
    /// <returns>Status message.</returns>
    [HttpPost]
    public IActionResult SetLogLevel([FromQuery] string level)
    {
        if (!Enum.TryParse<LogEventLevel>(level, true, out var newLevel))
        {
            return BadRequest("Invalid log level. Use one of: Verbose, Debug, Information, Warning, Error, Fatal");
        }
        inMemoryLogLevelSwitch.MinimumLevel = newLevel;
        return Ok($"In-memory sink log level changed to {newLevel}");
    }

    [HttpGet]
    public async Task<IActionResult> GetFleetTelemetryConfiguration(string vin)
    {
        var config = await fleetTelemetryConfigurationService.GetFleetTelemetryConfiguration(vin);
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
