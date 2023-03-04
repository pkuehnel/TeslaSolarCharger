using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace Plugins.Modbus.Controllers;

public class HelloController : ApiBaseController
{
    [HttpGet]
    public Task<bool> IsAlive() => Task.FromResult(true);
}
