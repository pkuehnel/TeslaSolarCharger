using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.SharedBackend.Abstracts;
using TeslaSolarCharger.SharedBackend.Contracts;
using TeslaSolarCharger.SharedBackend.Dtos;

namespace Plugins.Solax.Controllers;

public class CurrentValuesController : ApiBaseController
{
    private readonly ICurrentValuesService _currentValuesService;

    public CurrentValuesController(ICurrentValuesService currentValuesService)
    {
        _currentValuesService = currentValuesService;
    }

    [HttpGet]
    public Task<DtoCurrentPvValues> GetCurrentPvValues() => _currentValuesService.GetCurrentPvValues();
}
