using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class EnergyDataController(IEnergyDataService energyDataService) : ApiBaseController
{
    [HttpGet]
    public async Task<IActionResult> GetSolarPrediction(DateTimeOffset startDate, DateTimeOffset endDate, TimeSpan sliceLength)
    {
        var result = await energyDataService.GetPredictedSolarProductionByLocalHour(startDate, endDate, sliceLength, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetHouseConsumptionPrediction(DateTimeOffset startDate, DateTimeOffset endDate, TimeSpan sliceLength)
    {
        var result = await energyDataService.GetPredictedHouseConsumptionByLocalHour(startDate, endDate, sliceLength, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetSolarActual(DateTimeOffset startDate, DateTimeOffset endDate, TimeSpan sliceLength)
    {
        var result = await energyDataService.GetActualSolarProductionByLocalHour(startDate, endDate, sliceLength, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetHouseActual(DateTimeOffset startDate, DateTimeOffset endDate, TimeSpan sliceLength)
    {
        var result = await energyDataService.GetActualHouseConsumptionByLocalHour(startDate, endDate, sliceLength, HttpContext.RequestAborted);
        return Ok(result);
    }
}
