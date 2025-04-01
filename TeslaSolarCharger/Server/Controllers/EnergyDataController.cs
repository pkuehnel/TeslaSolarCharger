using Microsoft.AspNetCore.Mvc;
using MudBlazor;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class EnergyDataController(IEnergyDataService energyDataService) : ApiBaseController
{
    [HttpGet]
    public async Task<IActionResult> GetSolarPrediction(DateOnly? date = default)
    {
        if (date == default)
        {
            date = new DateOnly(2025, 3, 30);
        }

        var result = await energyDataService.GetPredictedSolarProductionByLocalHour(date.Value);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetHouseConsumptionPrediction(DateOnly? date = default)
    {
        if (date == default)
        {
            date = new DateOnly(2025, 3, 30);
        }

        var result = await energyDataService.GetPredictedHouseConsumptionByLocalHour(date.Value);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetSolarActual(DateOnly? date = default)
    {
        if (date == default)
        {
            date = new DateOnly(2025, 3, 30);
        }

        var result = await energyDataService.GetActualSolarProductionByLocalHour(date.Value);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetHouseActual(DateOnly? date = default)
    {
        if (date == default)
        {
            date = new DateOnly(2025, 3, 30);
        }

        var result = await energyDataService.GetActualHouseConsumptionByLocalHour(date.Value);
        return Ok(result);
    }
}
