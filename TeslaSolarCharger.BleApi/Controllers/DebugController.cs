using Microsoft.AspNetCore.Mvc;
using PkSoftwareService.Custom.Backend;
using Serilog.Core;
using Serilog.Events;
using System.Text;
using TeslaSolarCharger.BleApi.Abstracts;
using TeslaSolarCharger.BleApi.Dtos;

namespace TeslaSolarCharger.BleApi.Controllers;

public class DebugController(IInMemorySink inMemorySink, LoggingLevelSwitch inMemoryLogLevelSwitch) : ApiBaseController
{
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
