using Microsoft.AspNetCore.Mvc;
using Plugins.SolarEdge.Contracts;

namespace Plugins.SolarEdge.Controllers;

[Route("api/[controller]/[action]")]
[ApiController]
public class CurrentValuesController : ControllerBase
{
    private readonly ICurrentValuesService _currentValuesService;

    public CurrentValuesController(ICurrentValuesService currentValuesService)
    {
        _currentValuesService = currentValuesService;
    }

    [HttpGet]
    public Task<int> GetPowerToGrid()
    {
        return _currentValuesService.GetCurrentPowerToGrid();
    }

    [HttpGet]
    public Task<int> GetInverterPower()
    {
        return _currentValuesService.GetInverterPower();
    }
}