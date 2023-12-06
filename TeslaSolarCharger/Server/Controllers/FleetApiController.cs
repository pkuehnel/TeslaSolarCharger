using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Contracts;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class FleetApiController : ApiBaseController
{
    private readonly ITeslaFleetApiService _fleetApiService;
    private readonly IBackendApiService _backendApiService;
    private readonly ITeslaService _teslaService;
    private readonly IConfigurationWrapper _configurationWrapper;

    public FleetApiController(ITeslaFleetApiService fleetApiService, IBackendApiService backendApiService, ITeslaService teslaService,
        IConfigurationWrapper configurationWrapper)
    {
        _fleetApiService = fleetApiService;
        _backendApiService = backendApiService;
        _teslaService = teslaService;
        _configurationWrapper = configurationWrapper;
    }

    [HttpGet]
    public Task<DtoValue<FleetApiTokenState>> FleetApiTokenState() => _fleetApiService.GetFleetApiTokenState();

    [HttpGet]
    public Task<DtoValue<string>> GetOauthUrl(string locale) => _backendApiService.StartTeslaOAuth(locale);

    [HttpGet]
    public Task RefreshFleetApiToken() => _fleetApiService.RefreshTokenAsync();

    /// <summary>
    /// Note: This endpoint is only available in development environment
    /// </summary>
    /// <exception cref="InvalidOperationException">Is thrown when not beeing in dev Mode</exception>
    [HttpGet]
    public Task SetChargeLimit(int carId, int percent)
    {
        if (!_configurationWrapper.IsDevelopmentEnvironment())
        {
            throw new InvalidOperationException("This method is only available in development environment");
        }
        return _teslaService.SetChargeLimit(carId, percent);
    }
}
