using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class LiveDataController : ApiBaseController
{
    private readonly IStateSnapshotService _stateSnapshotService;
    private readonly IChangeTrackingService _changeTrackingService;

    public LiveDataController(IStateSnapshotService stateSnapshotService, IChangeTrackingService changeTrackingService)
    {
        _stateSnapshotService = stateSnapshotService;
        _changeTrackingService = changeTrackingService;
    }

    /// <summary>
    /// Get a list of all objects that are transferred via SignalR to the client.
    /// </summary>
    /// <returns>List of all objects that are transferred via SignalR to the client.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAllLatestStates()
    {
        var result = await _stateSnapshotService.GetAllCurrentStatesAsync();
        var changeTrackingResults = _changeTrackingService.GetLatestStates();
        foreach (var changeTrackingResult in changeTrackingResults)
        {
            result[changeTrackingResult.Key] = changeTrackingResult.Value;
        }

        return Ok(result);
    }

    /// <summary>
    /// Get one specific object that is normally transferred via SignalR to the client.
    /// </summary>
    /// <param name="dataType">The datatype you are interested in, e.g. if the key is LoadPointOverviewValues:3_null you need to enter LoadPointOverviewValues here</param>
    /// <param name="entityId">The entity type you are interested in, e.g. if the key is LoadPointOverviewValues:3_null you need to enter 3_null here. If the key has no entity ID e.g. for PvValues you can leave this blank.</param>
    /// <returns>one specific object that is normally transferred via SignalR to the client.</returns>
    [HttpGet]
    public async Task<IActionResult> GetLatestStateForKey(string dataType, string entityId)
    {
        var result = await _stateSnapshotService.GetCurrentStateAsync(dataType, entityId);
        if (result == null)
        {
            return NotFound($"No state found for data type '{dataType}' and entity ID '{entityId}'.");
        }
        return Ok(result);
    }

}
