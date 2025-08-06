using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class LiveDataController : ApiBaseController
{
    private readonly IChangeTrackingService _changeTrackingService;

    public LiveDataController(IChangeTrackingService changeTrackingService)
    {
        _changeTrackingService = changeTrackingService;
    }

    [HttpGet]
    public IActionResult GetLatestState()
    {
        var result = _changeTrackingService.GetLatestStates();
        return Ok(result);
    }
}
