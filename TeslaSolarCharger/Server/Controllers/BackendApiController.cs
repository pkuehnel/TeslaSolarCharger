using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class BackendApiController (IBackendApiService backendApiService, ITokenHelper tokenHelper) : ApiBaseController
{
    [HttpGet]
    public async Task<DtoValue<TokenState>> GetTokenState(bool useCache)
    {
        return new(await tokenHelper.GetBackendTokenState(useCache));
    }

    [HttpPost]
    public Task LoginToBackend(DtoBackendLogin login) => backendApiService.GetToken(login);
}
