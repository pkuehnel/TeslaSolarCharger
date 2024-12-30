using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Client.Services;

public class FleetApiTokenCheckService(ILogger<FleetApiTokenCheckService> logger, IHttpClientHelper httpClientHelper) : IFleetApiTokenCheckService
{
    public async Task<TokenState> HasValidBackendToken()
    {
        var response = await httpClientHelper.SendGetRequestWithSnackbarAsync<DtoValue<TokenState>>("api/BackendApi/HasValidBackendToken");
        return response?.Value ?? TokenState.MissingPrecondition;
    }
}
