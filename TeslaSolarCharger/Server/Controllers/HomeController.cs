using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Helper.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Home;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class HomeController : ApiBaseController
{
    private readonly IHomeService _homeService;
    private readonly ILoadPointManagementService _loadPointManagementService;
    private readonly ITeslaService _teslaService;
    private readonly INotChargingWithExpectedPowerReasonHelper _notChargingWithExpectedPowerReasonHelper;

    public HomeController(IHomeService homeService,
        ILoadPointManagementService loadPointManagementService,
        ITeslaService teslaService,
        INotChargingWithExpectedPowerReasonHelper notChargingWithExpectedPowerReasonHelper)
    {
        _homeService = homeService;
        _loadPointManagementService = loadPointManagementService;
        _teslaService = teslaService;
        _notChargingWithExpectedPowerReasonHelper = notChargingWithExpectedPowerReasonHelper;
    }

    [HttpGet]
    public async Task<IActionResult> GetLoadPointsToManage()
    {
        var result = await _loadPointManagementService.GetLoadPointsToManage().ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetCarChargingTargets(int carId)
    {
        var result = await _homeService.GetCarChargingTargets(carId);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetCarOverview(int carId)
    {
        var result = await _homeService.GetCarOverview(carId);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetChargingConnectorOverview(int chargingConnectorId)
    {
        var result = await _homeService.GetChargingConnectorOverview(chargingConnectorId);
        return Ok(result);
    }

    [HttpGet]
    public IActionResult GetChargingSchedules(int? carId, int? chargingConnectorId)
    {
        var result = _homeService.GetChargingSchedules(carId, chargingConnectorId);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetCarChargingTarget(int chargingTargetId)
    {
        var result = await _homeService.GetChargingTarget(chargingTargetId);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> SaveCarChargingTarget(int carId, [FromBody] DtoCarChargingTarget dto)
    {
        var result = await _homeService.SaveCarChargingTarget(carId, dto);
        return Ok(result);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteCarChargingTarget(int chargingTargetId)
    {
        await _homeService.DeleteCarChargingTarget(chargingTargetId);
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> UpdateCarMinSoc(int carId, int minSoc)
    {
        await _homeService.UpdateCarMinSoc(carId, minSoc);
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> UpdateCarMaxSoc(int carId, int soc)
    {
        await _homeService.UpdateCarMaxSoc(carId, soc);
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> UpdateCarChargeMode(int carId, ChargeModeV2 chargeMode)
    {
        await _homeService.UpdateCarChargeMode(carId, chargeMode);
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> UpdateChargingConnectorChargeMode(int chargingConnectorId, ChargeModeV2 chargeMode)
    {
        await _homeService.UpdateChargingConnectorChargeMode(chargingConnectorId, chargeMode);
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> StartChargingConnectorCharging(int chargingConnectorId, int currentToSet, int? numberOfPhases)
    {
        await _homeService.StartChargingConnectorCharging(chargingConnectorId, currentToSet, numberOfPhases, HttpContext.RequestAborted);
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> SetChargingConnectorCurrent(int chargingConnectorId, int currentToSet, int? numberOfPhases)
    {
        await _homeService.SetChargingConnectorCurrent(chargingConnectorId, currentToSet, numberOfPhases, HttpContext.RequestAborted);
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> StopChargingConnectorCharging(int chargingConnectorId)
    {
        await _homeService.StopChargingConnectorCharging(chargingConnectorId, HttpContext.RequestAborted);
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> SetCarChargingCurrent(int carId, int currentToSet)
    {
        await _teslaService.SetAmp(carId, currentToSet);
        return Ok();
    }

    [HttpGet]
    public IActionResult GetNotChargingWithExpectedPowerReasons(int? carId, int? connectorId)
    {
        var result = _notChargingWithExpectedPowerReasonHelper.GetReasons(carId, connectorId);
        return Ok(result);
    }

    [HttpGet]
    public IActionResult GetLoadPointCarOptions()
    {
        var result = _homeService.GetLoadPointCarOptions();
        return Ok(result);
    }

    [HttpPost]
    public IActionResult UpdateCarForLoadpoint(int chargingConnectorId, int? carId)
    {
        _loadPointManagementService.UpdateChargingConnectorCar(chargingConnectorId, carId);
        return Ok();
    }

    //[HttpGet]
    //public async Task<IActionResult> GetChargingSchedulesForLoadPoint(int? carId, int? chargingConnectorId)
    //{
    //    var result = await _chargingServiceV2.GetLatestPossibleChargingSchedule(carId, chargingConnectorId, HttpContext.RequestAborted);
    //    return Ok(result);
    //}
}
