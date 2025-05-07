using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.ChargepointAction;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class OcppTestController(
    IChargePointActionService ocppChargePointActionService,
    IOcppChargePointConfigurationService ocppChargePointConfigurationService) : ApiBaseController
{
    [HttpPost]
    public async Task<IActionResult> StartOcppCharging(string chargepointId, int connectorId, decimal current)
    {
        var result = await ocppChargePointActionService.StartCharging(chargepointId + "_" + connectorId, current, 3, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> StopOcppCharging(string chargepointId, int connectorId)
    {
        var result = await ocppChargePointActionService.StopCharging(chargepointId + "_" + connectorId, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> SetChargingCurrent(string chargepointId, int connectorId, decimal current)
    {
        var result = await ocppChargePointActionService.SetChargingCurrent(chargepointId + "_" + connectorId, current, 3, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetChargePointConfigurationKeys(string chargepointId)
    {
        var result = await ocppChargePointConfigurationService.GetOcppConfigurations(chargepointId, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> SetMeterValuesSampledDataConfiguration(string chargepointId)
    {
        var result = await ocppChargePointConfigurationService.SetMeterValuesSampledDataConfiguration(chargepointId, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> SetMeterValuesSampleIntervalConfiguration(string chargepointId)
    {
        var result = await ocppChargePointConfigurationService.SetMeterValuesSampleIntervalConfiguration(chargepointId, HttpContext.RequestAborted);
        return Ok(result);
    }
}
