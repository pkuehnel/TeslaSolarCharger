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
    public Task<DtoValue<FleetApiTokenState>> FleetApiTokenState() => fleetApiService.GetFleetApiTokenState();

    [HttpGet]
    public Task<DtoValue<string>> GetOauthUrl(string locale, string baseUrl) => backendApiService.StartTeslaOAuth(locale, baseUrl);

    [HttpGet]
    public Task RefreshFleetApiToken() => fleetApiService.GetNewTokenFromBackend();

    /// <summary>
    /// Note: This endpoint is only available in development environment
    /// </summary>
    /// <exception cref="InvalidOperationException">Is thrown when not beeing in dev Mode</exception>
    [HttpGet]
    public Task SetChargeLimit(int carId, int percent)
    {
        if (!configurationWrapper.IsDevelopmentEnvironment())
        {
            throw new InvalidOperationException("This method is only available in development environment");
        }
        return teslaService.SetChargeLimit(carId, percent);
    }

    [HttpGet]
    public Task<DtoValue<bool>> TestFleetApiAccess(int carId) => fleetApiService.TestFleetApiAccess(carId);
    [HttpGet]
    public DtoValue<bool> IsFleetApiEnabled() => fleetApiService.IsFleetApiEnabled();
    [HttpGet]
    public Task<DtoValue<bool>> IsFleetApiProxyEnabled(string vin) => fleetApiService.IsFleetApiProxyEnabled(vin);

    [HttpGet]
    public async Task<IActionResult> GetNewCarsInAccount()
    {
        var result = await fleetApiService.GetNewCarsInAccount().ConfigureAwait(false);
        return result.ToOk();
    }
}
