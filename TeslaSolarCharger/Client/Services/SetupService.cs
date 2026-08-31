using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Setup;

namespace TeslaSolarCharger.Client.Services;

public class SetupService(IHttpClientHelper httpClientHelper) : ISetupService
{
    public async Task<DtoSetupCache?> GetSetupCache()
    {
        return await httpClientHelper.SendGetRequestWithSnackbarAsync<DtoSetupCache>("api/SetupCache/GetSetupCache");
    }

    public async Task UpdateSetupCache(DtoSetupCache setupCache)
    {
        await httpClientHelper.SendPostRequestWithSnackbarAsync<object>("api/SetupCache/UpdateSetupCache", setupCache);
    }

    public async Task DeleteSetupCache()
    {
        await httpClientHelper.SendDeleteRequestWithSnackbarAsync<object>("api/SetupCache/DeleteSetupCache");
    }
}
