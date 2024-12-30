using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class BackendApiController (IBackendApiService backendApiService, ITeslaFleetApiTokenHelper tokenHelper) : ApiBaseController
{
    [HttpGet]
    public async Task<DtoValue<TokenState>> HasValidBackendToken()
    {
        return new(await tokenHelper.GetBackendTokenState(false));
    }

    [HttpPost]
    public Task LoginToBackend(DtoBackendLogin login) => backendApiService.GetToken(login);
}
