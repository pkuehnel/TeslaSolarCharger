using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Model.Enums;
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
        var result = await energyDataService.GetActualDataByLocalHour(MeterValueKind.SolarGeneration, startDate, endDate, sliceLength, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetHouseActual(DateTimeOffset startDate, DateTimeOffset endDate, TimeSpan sliceLength)
    {
        var result = await energyDataService.GetActualDataByLocalHour(MeterValueKind.HouseConsumption, startDate, endDate, sliceLength, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetHomeBatteryChargingActual(DateTimeOffset startDate, DateTimeOffset endDate, TimeSpan sliceLength)
    {
        var result = await energyDataService.GetActualDataByLocalHour(MeterValueKind.HomeBatteryCharging, startDate, endDate, sliceLength, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetHomeBatteryDischargingActual(DateTimeOffset startDate, DateTimeOffset endDate, TimeSpan sliceLength)
    {
        var result = await energyDataService.GetActualDataByLocalHour(MeterValueKind.HomeBatteryDischarging, startDate, endDate, sliceLength, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetPowerToGridActual(DateTimeOffset startDate, DateTimeOffset endDate, TimeSpan sliceLength)
    {
        var result = await energyDataService.GetActualDataByLocalHour(MeterValueKind.PowerToGrid, startDate, endDate, sliceLength, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetPowerFromGridActual(DateTimeOffset startDate, DateTimeOffset endDate, TimeSpan sliceLength)
    {
        var result = await energyDataService.GetActualDataByLocalHour(MeterValueKind.PowerFromGrid, startDate, endDate, sliceLength, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetHomeBatterySocActual(DateTimeOffset startDate, DateTimeOffset endDate, TimeSpan sliceLength)
    {
        var result = await energyDataService.GetActualHomeBatterySocByLocalHour(startDate, endDate, sliceLength, HttpContext.RequestAborted);
        return Ok(result);
    }
}
