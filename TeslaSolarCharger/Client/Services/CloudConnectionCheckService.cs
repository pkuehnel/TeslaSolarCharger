using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Client.Services;

public class CloudConnectionCheckService(ILogger<CloudConnectionCheckService> logger, IHttpClientHelper httpClientHelper) : ICloudConnectionCheckService
{
    public async Task<TokenState> GetBackendTokenState(bool useCache)
    {
        logger.LogTrace("{method}({useCache})", nameof(GetBackendTokenState), useCache);
        var response = await httpClientHelper.SendGetRequestWithSnackbarAsync<DtoValue<TokenState>>($"api/BackendApi/GetTokenState?useCache={useCache}");
        return response?.Value ?? TokenState.MissingPrecondition;
    }

    public async Task<string?> GetBackendTokenUserName()
    {
        logger.LogTrace("{method}()", nameof(GetBackendTokenUserName));
        var response = await httpClientHelper.SendGetRequestWithSnackbarAsync<DtoValue<string?>>("api/BackendApi/GetTokenUserName");
        return response?.Value;
    }

    public async Task<TokenState> GetFleetApiTokenState(bool useCache)
    {
        logger.LogTrace("{method}({useCache})", nameof(GetFleetApiTokenState), useCache);
        var response = await httpClientHelper.SendGetRequestWithSnackbarAsync<DtoValue<TokenState>>($"api/FleetApi/FleetApiTokenState?useCache={useCache}");
        return response?.Value ?? TokenState.MissingPrecondition;
    }

    public async Task<string?> GetTeslaLoginUrl(string locale, string baseUrl)
    {
        logger.LogTrace("{method}({locale}, {baseUrl})", nameof(GetTeslaLoginUrl), locale, baseUrl);
        var response = await httpClientHelper.SendGetRequestWithSnackbarAsync<DtoValue<string>>($"api/FleetApi/GetOauthUrl?locale={Uri.EscapeDataString(locale)}&baseUrl={Uri.EscapeDataString(baseUrl)}");
        return response?.Value;
    }
}
