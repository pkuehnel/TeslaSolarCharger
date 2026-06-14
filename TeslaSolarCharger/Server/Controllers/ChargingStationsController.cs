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

    [HttpGet]
    public async Task<IActionResult> GetCarOptions()
    {
        return Ok(await _ocppChargingStationConfigurationService.GetCarOptions());
    }

    [HttpPost]
    public async Task<IActionResult> UpdateChargingStationConnector([FromBody] DtoChargingStationConnector chargingStationConnector)
    {
        await _ocppChargingStationConfigurationService.UpdateChargingStationConnector(chargingStationConnector).ConfigureAwait(false);
        return Ok();
    }

    /// <summary>
    /// Delete a charging station together with all of its connectors and their related data (charging
    /// processes, OCPP transactions, connector value logs, meter values and allowed car assignments).
    /// Charging processes that also belong to a car are kept as car history (only the connector reference is
    /// removed).
    /// </summary>
    /// <param name="chargingStationId">Id of the charging station to delete</param>
    [HttpDelete]
    public async Task<IActionResult> DeleteChargingStation(int chargingStationId)
    {
        await _ocppChargingStationConfigurationService.DeleteChargingStation(chargingStationId).ConfigureAwait(false);
        return Ok();
    }
}
