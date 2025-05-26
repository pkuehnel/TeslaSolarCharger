using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class HomeController : ApiBaseController
{
    private readonly IHomeService _homeService;
    private readonly IChargingServiceV2 _chargingServiceV2;

    public HomeController(IHomeService homeService, IChargingServiceV2 chargingServiceV2)
    {
        _homeService = homeService;
        _chargingServiceV2 = chargingServiceV2;
    }

    [HttpGet]
    public async Task<IActionResult> GetLoadPointOverviews()
    {
        var result = await _homeService.GetLoadPointOverviews();
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetCarChargingTargets(int carId)
    {
        var result = await _homeService.GetCarChargingTargets(carId);
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

    [HttpGet]
    public async Task<IActionResult> GetChargingSchedulesForLoadPoint(int? carId, int? chargingConnectorId)
    {
        var result = await _chargingServiceV2.GetChargingSchedulesForLoadPoint(carId, chargingConnectorId, HttpContext.RequestAborted);
        return Ok(result);
    }
}
