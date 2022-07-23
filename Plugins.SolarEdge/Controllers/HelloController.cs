using Microsoft.AspNetCore.Mvc;

namespace Plugins.SolarEdge.Controllers;

[Route("api/[controller]/[action]")]
[ApiController]
public class HelloController : ControllerBase
{
    [HttpGet]
    public Task<bool> IsAlive() => Task.FromResult(true);
}