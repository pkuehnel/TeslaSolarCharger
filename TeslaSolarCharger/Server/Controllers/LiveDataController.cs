using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class LiveDataController : ApiBaseController
{
    private readonly IStateSnapshotService _stateSnapshotService;

    public LiveDataController(IStateSnapshotService stateSnapshotService)
    {
        _stateSnapshotService = stateSnapshotService;
    }

    [HttpGet]
    public async Task<IActionResult> GetLatestState()
    {
        var result = await _stateSnapshotService.GetAllCurrentStatesAsync();
        return Ok(result);
    }
}
