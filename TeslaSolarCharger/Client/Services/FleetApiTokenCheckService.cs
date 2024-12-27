using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos;

namespace TeslaSolarCharger.Client.Services;

public class FleetApiTokenCheckService(ILogger<FleetApiTokenCheckService> logger, IHttpClientHelper httpClientHelper) : IFleetApiTokenCheckService
{
    public async Task<bool> HasValidBackendToken()
    {
        try
        {
            var response = await httpClientHelper.SendGetRequestWithSnackbarAsync<DtoValue<bool>>("api/BackendApi/HasValidBackendToken");
            return response?.Value ?? false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while checking backend token");
            return false;
        }
    }
}
