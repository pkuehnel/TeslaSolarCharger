using Microsoft.AspNetCore.Mvc;

namespace Plugins.SolarEdge.Controllers;

[Route("api/[controller]/[action]")]
[ApiController]
public class CurrentValuesController : ControllerBase
{

    [HttpGet]
    public int GetPower()
    {
        return _currentPowerService.GetCurrentPower();
    }
}