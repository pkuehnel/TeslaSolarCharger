using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Ble;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class BleController (IBleService bleService) : ApiBaseController
{
    [HttpGet]
    public Task<DtoBleCommandResult> PairKey(string vin, string apiRole) => bleService.PairKey(vin, apiRole);

    [HttpGet]
    public Task<DtoBleCommandResult> StartCharging(string vin) => bleService.StartCharging(vin);

    [HttpGet]
    public Task<DtoBleCommandResult> StopCharging(string vin) => bleService.StopCharging(vin);

    [HttpGet]
    public Task<DtoBleCommandResult> SetAmp(string vin, int amps) => bleService.SetAmp(vin, amps);

    [HttpGet]
    public Task<DtoBleCommandResult> FlashLights(string vin) => bleService.FlashLights(vin);

    [HttpGet]
    public Task<DtoBleCommandResult> WakeUp(string vin) => bleService.WakeUpCar(vin);

    [HttpGet]
    public Task<DtoBleCommandResult> GetChargeState(string vin) => bleService.GetChargeState(vin);

    [HttpGet]
    public Task<DtoBleCommandResult> BeaconScan(string vin) => bleService.GetBeaconScanResult(vin);

    [HttpGet]
    public Task<DtoBleCommandResult> GetDriveState(string vin) => bleService.GetDriveState(vin);

    [HttpGet]
    public ActionResult<List<DtoBleContainer>> GetBleContainers() => bleService.GetBleContainers();

    [HttpGet]
    public async Task<IActionResult> DownloadLogs(string bleApiBaseUrl)
    {
        var stream = await bleService.DownloadLogs(bleApiBaseUrl);
        if (stream == default)
        {
            return NotFound("Could not download logs from the BLE container. Make sure the BLE URL is configured for a car and the container is reachable and up to date.");
        }
        return File(stream, "text/plain", "ble-logs.log");
    }
}
