using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class FleetApiController : ApiBaseController
{
    private readonly ITeslaFleetApiService _fleetApiService;
    private readonly IBackendApiService _backendApiService;

    public FleetApiController(ITeslaFleetApiService fleetApiService, IBackendApiService backendApiService)
    {
        _fleetApiService = fleetApiService;
        _backendApiService = backendApiService;
    }

    [HttpGet]
    public Task<DtoValue<FleetApiTokenState>> FleetApiTokenState() => _fleetApiService.GetFleetApiTokenState();

    [HttpGet]
    public Task<DtoValue<string>> GetOauthUrl(string locale) => _backendApiService.StartTeslaOAuth(locale);
}
