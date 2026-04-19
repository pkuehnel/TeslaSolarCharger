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

    [HttpGet]
    public async Task<DtoValue<string?>> GetTokenUserName()
    {
        return new(await tokenHelper.GetTokenUserName());
    }

    [HttpPost]
    public Task LoginToBackend(DtoBackendLogin login) => backendApiService.GetToken(login);

    [HttpGet]
    public async Task<IActionResult> GetTeslaOAuthRedeemUrl(string baseUrl)
    {
        var result = await backendApiService.GetTeslaOAuthRedeemUrlIncludingCookieAuthCode(baseUrl);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetSmartCarOAuthRedeemUrl(string baseUrl, string vin)
    {
        var result = await backendApiService.GetSmartCarOAuthRedeemUrlIncludingCookieAuthCode(baseUrl, vin);
        return Ok(result);
    }
}
