using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using System.Net;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Dtos.Car;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedBackend.Abstracts;
using TeslaSolarCharger.SharedBackend.Extensions;

namespace TeslaSolarCharger.Server.Controllers;

public class FleetApiController(
    ITeslaFleetApiService fleetApiService,
    IBackendApiService backendApiService,
    ITeslaService teslaService,
    IConfigurationWrapper configurationWrapper)
    : ApiBaseController
{
    [HttpGet]
    public Task<DtoValue<TokenState>> FleetApiTokenState(bool useCache) => fleetApiService.GetFleetApiTokenState(useCache);

    [HttpGet]
    public Task<DtoValue<string>> GetOauthUrl(string locale, string baseUrl) => backendApiService.StartTeslaOAuth(locale, baseUrl);

    [HttpGet]
    public async Task<IActionResult> GetFleetApiState(int carId)
    {
        var result = await fleetApiService.GetFleetApiState(carId);
        return Ok(new DtoValue<TeslaCarFleetApiState?>(result));
    }

    [HttpGet]
    public Task<DtoValue<bool>> TestFleetApiAccess(int carId) => fleetApiService.TestFleetApiAccess(carId);
}
