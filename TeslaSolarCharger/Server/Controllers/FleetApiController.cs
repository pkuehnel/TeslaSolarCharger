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
    public Task<DtoValue<TokenState>> FleetApiTokenState() => fleetApiService.GetFleetApiTokenState();

    [HttpGet]
    public Task<DtoValue<string>> GetOauthUrl(string locale, string baseUrl) => backendApiService.StartTeslaOAuth(locale, baseUrl);

    [HttpGet]
    public Task<DtoValue<bool>> TestFleetApiAccess(int carId) => fleetApiService.TestFleetApiAccess(carId);
    [HttpGet]
    public Task<DtoValue<bool>> IsFleetApiProxyEnabled(string vin) => fleetApiService.IsFleetApiProxyEnabled(vin);

    [HttpGet]
    public async Task<IActionResult> GetNewCarsInAccount()
    {
        var result = await fleetApiService.GetNewCarsInAccount().ConfigureAwait(false);
        return result.ToOk();
    }
}
