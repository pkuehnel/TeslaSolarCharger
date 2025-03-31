using Microsoft.AspNetCore.Mvc;
using MudBlazor;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class EnergyPredictionController(IEnergyPredictionService energyPredictionService) : ApiBaseController
{
    [HttpGet]
    public async Task<IActionResult> GetSolarPrediction(DateOnly? date = default)
    {
        if (date == default)
        {
            date = new DateOnly(2025, 3, 30);
        }

        var result = await energyPredictionService.GetPredictedSolarProductionByLocalHour(date.Value);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetHouseConsumptionPrediction(DateOnly? date = default)
    {
        if (date == default)
        {
            date = new DateOnly(2025, 3, 30);
        }

        var result = await energyPredictionService.GetPredictedHouseConsumptionByLocalHour(date.Value);
        return Ok(result);
    }
}
