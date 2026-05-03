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

    [HttpPost]
    public async Task<IActionResult> ConnectCarToSmartCar(string vin)
    {
        await backendApiService.ConnectCarToSmartCarByVin(vin);
        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> GetAuthorizeUrl(string baseUrl)
    {
        var result = await backendApiService.GetAuthorizeUrl(baseUrl);
        return Ok(new DtoValue<string>(result));
    }

    [HttpPost]
    public async Task<IActionResult> ExchangeToken(string code, string state, string baseUrl)
    {
        await backendApiService.ExchangeToken(code, state, baseUrl);
        return Ok();
    }
}
