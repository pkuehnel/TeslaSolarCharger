using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedBackend.Abstracts;

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
    public Task<DtoValue<string>> GetOauthUrl(string locale) => backendApiService.StartTeslaOAuth(locale);

    [HttpGet]
    public Task RefreshFleetApiToken() => fleetApiService.RefreshTokenAsync();

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
    public DtoValue<bool> IsFleetApiProxyEnabled() => fleetApiService.IsFleetApiProxyEnabled();
}
