using TeslaSolarCharger.Client.Helper.Contracts;
using TeslaSolarCharger.Client.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Setup;

namespace TeslaSolarCharger.Client.Services;

public class SetupService(IHttpClientHelper httpClientHelper) : ISetupService
{
    public async Task<DtoSetupState?> GetSetupState()
    {
        return await httpClientHelper.SendGetRequestWithSnackbarAsync<DtoSetupState>("api/Setup/GetSetupState");
    }

    public async Task<DtoSetupState?> GetOrCreateSetupState()
    {
        return await httpClientHelper.SendGetRequestWithSnackbarAsync<DtoSetupState>("api/Setup/GetOrCreateSetupState");
    }

    public async Task<DtoSetupState?> SyncCarDrafts(DtoSetupState setupState)
    {
        return await httpClientHelper.SendPostRequestWithSnackbarAsync<DtoSetupState>("api/Setup/SyncCarDrafts", setupState);
    }

    public async Task<DtoSetupApplicationResult?> SaveCarDraft(DtoSetupState setupState, Guid draftId)
    {
        return await httpClientHelper.SendPostRequestWithSnackbarAsync<DtoSetupApplicationResult>(
            $"api/Setup/SaveCarDraft?draftId={draftId}", setupState);
    }

    public async Task<DtoSetupCarCapabilities?> GetCarCapabilities(int carId)
    {
        return await httpClientHelper.SendGetRequestWithSnackbarAsync<DtoSetupCarCapabilities>(
            $"api/Setup/GetCarCapabilities?carId={carId}");
    }

    public async Task UpdateSetupState(DtoSetupState setupState)
    {
        await httpClientHelper.SendPostRequestWithSnackbarAsync<object>("api/Setup/UpdateSetupState", setupState);
    }

    public async Task DeleteSetupState()
    {
        await httpClientHelper.SendDeleteRequestWithSnackbarAsync<object>("api/Setup/DeleteSetupState");
    }

    public async Task<DtoSetupDecision?> EvaluateSetupState(DtoSetupState setupState)
    {
        return await httpClientHelper.SendPostRequestWithSnackbarAsync<DtoSetupDecision>("api/Setup/EvaluateSetupState", setupState);
    }

    public async Task<DtoSetupState?> AcceptProposals(DtoSetupState setupState, List<DtoSetupProposedValue> proposals)
    {
        var request = new DtoSetupAcceptProposalsRequest { SetupState = setupState, Proposals = proposals, };
        return await httpClientHelper.SendPostRequestWithSnackbarAsync<DtoSetupState>("api/Setup/AcceptProposals", request);
    }

    public async Task<DtoSetupApplicationResult?> ApplyConfiguration(DtoSetupState setupState)
    {
        return await httpClientHelper.SendPostRequestWithSnackbarAsync<DtoSetupApplicationResult>("api/Setup/ApplyConfiguration", setupState);
    }

    public async Task<DtoSetupApplicationResult?> ActivateAndCompleteSetup(DtoSetupState setupState)
    {
        return await httpClientHelper.SendPostRequestWithSnackbarAsync<DtoSetupApplicationResult>("api/Setup/ActivateAndCompleteSetup", setupState);
    }
}
