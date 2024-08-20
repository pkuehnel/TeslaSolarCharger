using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Dtos.Ble;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Ble;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class BleController (IBleService bleService) : ApiBaseController
{
    [HttpGet]
    public Task<DtoBleResult> PairKey(string vin, string apiRole) => bleService.PairKey(vin, apiRole);

    [HttpGet]
    public Task<DtoBleResult> StartCharging(string vin) => bleService.StartCharging(vin);

    [HttpGet]
    public Task<DtoBleResult> StopCharging(string vin) => bleService.StopCharging(vin);

    [HttpGet]
    public Task<DtoBleResult> SetAmp(string vin, int amps) => bleService.SetAmp(vin, amps);

    [HttpGet]
    public Task<DtoBleResult> FlashLights(string vin) => bleService.FlashLights(vin);

    [HttpGet]
    public Task<DtoBleResult> WakeUp(string vin) => bleService.WakeUpCar(vin);
}
