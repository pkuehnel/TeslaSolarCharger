using Microsoft.AspNetCore.Mvc;
using MudBlazor;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class SolarProductionPredictionController(ISolarProductionPredictionService solarProductionPredictionService) : ApiBaseController
{
    [HttpGet]
    public async Task<IActionResult> GetSolarPrediction(DateOnly? date = default)
    {
        if (date == default)
        {
            date = new DateOnly(2025, 3, 31);
        }

        var result = await solarProductionPredictionService.GetPredictedSolarProductionByLocalHour(date.Value);
        return Ok(result);
    }
}
