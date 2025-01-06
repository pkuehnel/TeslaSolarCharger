using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Client.Services;

public class BackendApiTokenCheckService(ILogger<BackendApiTokenCheckService> logger, IHttpClientHelper httpClientHelper) : IBackendApiTokenCheckService
{
    public async Task<TokenState> GetTokenState(bool useCache)
    {
        var response = await httpClientHelper.SendGetRequestWithSnackbarAsync<DtoValue<TokenState>>($"api/BackendApi/GetTokenState?useCache={useCache}");
        return response?.Value ?? TokenState.MissingPrecondition;
    }
}
