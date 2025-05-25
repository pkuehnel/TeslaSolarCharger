using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class HomeController : ApiBaseController
{
    private readonly IHomeService _homeService;

    public HomeController(IHomeService homeService)
    {
        _homeService = homeService;
    }

    [HttpGet]
    public async Task<IActionResult> GetLoadPointOverviews()
    {
        var result = await _homeService.GetLoadPointOverviews();
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetCarChargingSchedules(int carId)
    {
        var result = await _homeService.GetCarChargingSchedules(carId);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetCarChargingSchedule(int chargingScheduleId)
    {
        var result = await _homeService.GetChargingSchedule(chargingScheduleId);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> SaveCarChargingSchedule(int carId, [FromBody] DtoCarChargingSchedule dto)
    {
        var result = await _homeService.SaveCarChargingSchedule(carId, dto);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateCarMinSoc(int carId, int minSoc)
    {
        await _homeService.UpdateCarMinSoc(carId, minSoc);
        return Ok();
    }

}
