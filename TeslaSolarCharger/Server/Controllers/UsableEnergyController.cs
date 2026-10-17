using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class UsableEnergyController(IUsableEnergyLearningService usableEnergyLearningService) : ApiBaseController
{
    [HttpGet]
    public async Task<IActionResult> GetCarState(int carId)
    {
        var result = await usableEnergyLearningService.GetCarStateAsync(carId, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetHomeBatteryState()
    {
        var result = await usableEnergyLearningService.GetHomeBatteryStateAsync(HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> ResetCar(int carId)
    {
        await usableEnergyLearningService.ResetCarAsync(carId, HttpContext.RequestAborted);
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> ResetHomeBattery()
    {
        await usableEnergyLearningService.ResetHomeBatteryAsync(HttpContext.RequestAborted);
        return Ok();
    }
}
