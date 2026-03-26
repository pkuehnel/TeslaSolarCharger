using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class FleetApiController(
    ITeslaFleetApiService fleetApiService,
    IBackendApiService backendApiService)
    : ApiBaseController
{
    [HttpGet]
    public Task<DtoValue<TokenState>> FleetApiTokenState(bool useCache) => fleetApiService.GetFleetApiTokenState(useCache);

    [HttpGet]
    public async Task<IActionResult> GetRedeemUrlIncludingCookieAuthCode(string baseUrl)
    {
        var result = await backendApiService.GetTeslaOAuthRedeemUrlIncludingCookieAuthCode(baseUrl);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetFleetApiState(int carId)
    {
        var result = await fleetApiService.GetFleetApiState(carId);
        return Ok(new DtoValue<TeslaCarFleetApiState?>(result));
    }

    [HttpGet]
    public Task<DtoValue<bool>> TestFleetApiAccess(int carId) => fleetApiService.TestFleetApiAccess(carId);

    [HttpGet]
    public async Task<IActionResult> GetEnergySites()
    {
        var result = await fleetApiService.GetEnergySites();
        return Ok(result);
    }
}
