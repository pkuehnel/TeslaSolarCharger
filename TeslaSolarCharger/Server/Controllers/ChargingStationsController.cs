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

    [HttpPost]
    public async Task<IActionResult> UpdateChargingStationConnector(DtoChargingStationConnector chargingStationConnector)
    {
        await _ocppChargingStationConfigurationService.UpdateChargingStationConnector(chargingStationConnector).ConfigureAwait(false);
        return Ok();
    }
}
