using Microsoft.AspNetCore.Mvc;
using PkSoftwareService.Custom.Backend;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class DebugController(InMemorySink inMemorySink, Serilog.Core.LoggingLevelSwitch inMemoryLogLevelSwitch/*chaning log level switch is not tested*/) : ApiBaseController
{
    [HttpGet]
    public IActionResult GetLogs()
    {
        var logs = inMemorySink.GetLogs();
        return Ok(logs);
    }
}
