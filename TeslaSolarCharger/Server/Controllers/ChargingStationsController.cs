using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.ChargingStation;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class ChargingStationsController : ApiBaseController
{
    private readonly IOcppChargingStationConfigurationService _ocppChargingStationConfigurationService;
    private readonly ISettings _settings;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ChargingStationsController> _logger;

    public ChargingStationsController(IOcppChargingStationConfigurationService ocppChargingStationConfigurationService,
        ISettings settings,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ChargingStationsController> logger)
    {
        _ocppChargingStationConfigurationService = ocppChargingStationConfigurationService;
        _settings = settings;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
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
    public IActionResult DeleteChargingStation(int chargingStationId)
    {
        // The deletion can take far longer than the client's HTTP timeout (the connector value log and meter
        // value tables can hold millions of rows), so run it as a background task and return at once. The UI
        // tracks completion by polling GetChargingStationDeletionProgress, which reports null again once the
        // deletion has finished. Set the progress synchronously here so the very first poll already sees a running
        // deletion (closing the race where the background task has not started yet). The background task always
        // clears it again in its finally - even if DeleteChargingStation returns early (station not found) or
        // throws. Progress is keyed by station id so deleting several stations does not clobber each other.
        _settings.ChargingStationDeletionProgresses[chargingStationId] = new DtoChargingStationDeletionProgress
        {
            Value = 0,
            MaxValue = 7,
            CurrentStep = ChargingStationDeletionStep.ChargingProcesses,
        };
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var scopedConfigurationService = scope.ServiceProvider.GetRequiredService<IOcppChargingStationConfigurationService>();
                await scopedConfigurationService.DeleteChargingStation(chargingStationId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not delete charging station {chargingStationId}", chargingStationId);
            }
            finally
            {
                _settings.ChargingStationDeletionProgresses.TryRemove(chargingStationId, out _);
            }
        });
        return Ok();
    }

    /// <summary>
    /// Get the progress of the currently running deletion of the given charging station (or null if no deletion
    /// is running for it). Polled by the UI to show what is being deleted at the moment.
    /// </summary>
    /// <param name="chargingStationId">Id of the charging station whose deletion progress to return</param>
    [HttpGet]
    public IActionResult GetChargingStationDeletionProgress(int chargingStationId)
    {
        return Ok(_settings.ChargingStationDeletionProgresses.GetValueOrDefault(chargingStationId));
    }
}
