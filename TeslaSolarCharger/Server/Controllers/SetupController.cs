using Microsoft.AspNetCore.Mvc;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Dtos.Setup;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.SharedBackend.Abstracts;

namespace TeslaSolarCharger.Server.Controllers;

public class SetupController(
    ISetupStateService setupStateService,
    ISetupDecisionService setupDecisionService,
    ISetupApplicationService setupApplicationService,
    IDeferredSetupCheckService deferredSetupCheckService)
    : ApiBaseController
{
    [HttpGet]
    public async Task<ActionResult<DtoSetupState?>> GetSetupState()
    {
        var setupState = await setupStateService.GetSetupState();
        if (setupState == null)
        {
            // Return 204 instead of a 200 with a null body: the client's snackbar-wrapped GET reports a
            // deserialized-null payload as an error, which would show a spurious error toast on a fresh setup
            // (where there is no state yet). 204 is mapped to "no value" by the client without an error.
            return NoContent();
        }

        return setupState;
    }

    [HttpGet]
    public async Task<ActionResult<DtoSetupState>> GetOrCreateSetupState()
    {
        return await setupStateService.GetOrCreateSetupState();
    }

    [HttpPost]
    public async Task<ActionResult> UpdateSetupState([FromBody] DtoSetupState setupState)
    {
        await setupStateService.UpdateSetupState(setupState);
        return Ok();
    }

    [HttpDelete]
    public async Task<ActionResult> DeleteSetupState()
    {
        await setupStateService.DeleteSetupState();
        return Ok();
    }

    [HttpPost]
    public async Task<ActionResult<DtoSetupDecision>> EvaluateSetupState([FromBody] DtoSetupState setupState)
    {
        return await setupDecisionService.Evaluate(setupState);
    }

    [HttpPost]
    public ActionResult<DtoSetupState> AcceptProposals([FromBody] DtoSetupAcceptProposalsRequest request)
    {
        return setupApplicationService.AcceptProposals(request.SetupState, request.Proposals);
    }

    [HttpPost]
    public async Task<ActionResult<DtoSetupApplicationResult>> ApplyConfiguration([FromBody] DtoSetupState setupState)
    {
        return await setupApplicationService.ApplyConfiguration(setupState);
    }

    [HttpPost]
    public async Task<ActionResult<DtoSetupApplicationResult>> ActivateAndCompleteSetup([FromBody] DtoSetupState setupState)
    {
        return await setupApplicationService.ActivateAndCompleteSetup(setupState);
    }

    [HttpGet]
    public async Task<ActionResult<List<DtoDeferredSetupCheck>>> GetDeferredChecks()
    {
        return await deferredSetupCheckService.GetDeferredChecks();
    }

    [HttpPost]
    public async Task<ActionResult<DtoDeferredSetupCheck>> AddOrUpdateDeferredCheck([FromBody] DtoDeferredSetupCheck check)
    {
        return await deferredSetupCheckService.AddOrUpdateDeferredCheck(check);
    }

    [HttpPost]
    public async Task<ActionResult> RecordDeferredCheckResult(Guid checkId, SetupCheckResultState state, string? message)
    {
        await deferredSetupCheckService.RecordCheckResult(checkId, state, message);
        return Ok();
    }

    [HttpDelete]
    public async Task<ActionResult> DeleteDeferredCheck(Guid checkId)
    {
        await deferredSetupCheckService.RemoveDeferredCheck(checkId);
        return Ok();
    }
}
