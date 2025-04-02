using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class EnergyDataController(IEnergyDataService energyDataService) : ApiBaseController
{
    [HttpGet]
    public async Task<IActionResult> GetSolarPrediction(DateOnly date)
    {
        var result = await energyDataService.GetPredictedSolarProductionByLocalHour(date);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetHouseConsumptionPrediction(DateOnly date)
    {
        var result = await energyDataService.GetPredictedHouseConsumptionByLocalHour(date);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetSolarActual(DateOnly date)
    {
        var result = await energyDataService.GetActualSolarProductionByLocalHour(date);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetHouseActual(DateOnly date)
    {
        var result = await energyDataService.GetActualHouseConsumptionByLocalHour(date);
        return Ok(result);
    }
}
