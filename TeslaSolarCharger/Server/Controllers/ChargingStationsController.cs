using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.ChargingStation;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class ChargingStationsController : ApiBaseController
{
    private readonly IOcppChargingStationConfigurationService _ocppChargingStationConfigurationService;

    public ChargingStationsController(IOcppChargingStationConfigurationService ocppChargingStationConfigurationService)
    {
        _ocppChargingStationConfigurationService = ocppChargingStationConfigurationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetChargingStations()
    {
        return Ok(await _ocppChargingStationConfigurationService.GetChargingStations());
    }

    [HttpGet]
    public async Task<IActionResult> GetChargingStationConnectors(int chargingStationId)
    {
        return Ok(await _ocppChargingStationConfigurationService.GetChargingStationConnectors(chargingStationId));
    }

    [HttpPost]
    public async Task<IActionResult> UpdateChargingStationConnector([FromBody] DtoChargingStationConnector chargingStationConnector)
    {
        await _ocppChargingStationConfigurationService.UpdateChargingStationConnector(chargingStationConnector).ConfigureAwait(false);
        return Ok();
    }
}
