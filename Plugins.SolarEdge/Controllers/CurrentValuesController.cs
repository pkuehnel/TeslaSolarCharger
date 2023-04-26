using Microsoft.AspNetCore.Mvc;
using Plugins.SolarEdge.Contracts;
using TeslaSolarCharger.SharedBackend.Abstracts;
using TeslaSolarCharger.SharedBackend.Dtos;

namespace Plugins.SolarEdge.Controllers;

public class CurrentValuesController : ApiBaseController
{
    private readonly ICurrentValuesService _currentValuesService;

    public CurrentValuesController(ICurrentValuesService currentValuesService)
    {
        _currentValuesService = currentValuesService;
    }

    [HttpGet]
    public Task<DtoCurrentPvValues> GetCurrentPvValues() => _currentValuesService.GetCurrentPvValues();

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

    [HttpGet]
    public Task<int?> GetHomeBatterySoc()
    {
        return _currentValuesService.GetHomeBatterySoc();
    }

    [HttpGet]
    public Task<int?> GetHomeBatteryPower()
    {
        return _currentValuesService.GetHomeBatteryPower();
    }
}
