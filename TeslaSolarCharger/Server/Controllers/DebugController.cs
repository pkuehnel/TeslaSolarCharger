using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class DebugController(ILogService logService) : ApiBaseController
{
    [HttpGet]
    public IActionResult GetLogs()
    {
        return Ok(new DtoValue<string>(logService.GetLogs()));
    }
}
