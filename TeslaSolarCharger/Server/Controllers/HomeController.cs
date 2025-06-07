using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class HomeController : ApiBaseController
{
    private readonly IHomeService _homeService;
    private readonly ILoadPointManagementService _loadPointManagementService;

    public HomeController(IHomeService homeService, ILoadPointManagementService loadPointManagementService)
    {
        _homeService = homeService;
        _loadPointManagementService = loadPointManagementService;
    }

    [HttpGet]
    public async Task<IActionResult> GetLoadPointsToManage()
    {
        var result = await _loadPointManagementService.GetLoadPointsToManage().ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetCarChargingTargets(int carId)
    {
        var result = await _homeService.GetCarChargingTargets(carId);
        return Ok(result);
    }

    [HttpGet]
    public IActionResult GetCarOverview(int carId)
    {
        var result = _homeService.GetCarOverview(carId);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetChargingConnectorOverview(int chargingConnectorId)
    {
        var result = await _homeService.GetChargingConnectorOverview(chargingConnectorId);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetCarChargingTarget(int chargingTargetId)
    {
        var result = await _homeService.GetChargingTarget(chargingTargetId);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> SaveCarChargingTarget(int carId, [FromBody] DtoCarChargingTarget dto)
    {
        var result = await _homeService.SaveCarChargingTarget(carId, dto);
        return Ok(result);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteCarChargingTarget(int chargingTargetId)
    {
        await _homeService.DeleteCarChargingTarget(chargingTargetId);
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> UpdateCarMinSoc(int carId, int minSoc)
    {
        await _homeService.UpdateCarMinSoc(carId, minSoc);
        return Ok();
    }

    //[HttpGet]
    //public async Task<IActionResult> GetChargingSchedulesForLoadPoint(int? carId, int? chargingConnectorId)
    //{
    //    var result = await _chargingServiceV2.GetLatestPossibleChargingSchedule(carId, chargingConnectorId, HttpContext.RequestAborted);
    //    return Ok(result);
    //}
}
