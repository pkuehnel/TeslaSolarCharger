using TeslaSolarCharger.Shared.Dtos.Setup;

namespace TeslaSolarCharger.Client.Services.Contracts;

public interface ISetupService
{
    /// <summary>The stored setup state, or null when setup has never been started or was already finished.</summary>
    Task<DtoSetupState?> GetSetupState();

    /// <summary>The stored setup state, or a fresh one seeded from what the installation already has configured.</summary>
    Task<DtoSetupState?> GetOrCreateSetupState();

    Task UpdateSetupState(DtoSetupState setupState);

    Task DeleteSetupState();

    /// <summary>Which steps apply, what to do next, and what is missing or can be decided automatically.</summary>
    Task<DtoSetupDecision?> EvaluateSetupState(DtoSetupState setupState);

    /// <summary>Writes the proposals the user agreed to into the state, without touching the live configuration.</summary>
    Task<DtoSetupState?> AcceptProposals(DtoSetupState setupState, List<DtoSetupProposedValue> proposals);

    /// <summary>Saves the configuration without activating anything.</summary>
    Task<DtoSetupApplicationResult?> ApplyConfiguration(DtoSetupState setupState);

    /// <summary>Saves, activates the chosen equipment and finishes setup. Reports per operation.</summary>
    Task<DtoSetupApplicationResult?> ActivateAndCompleteSetup(DtoSetupState setupState);

    Task<List<DtoDeferredSetupCheck>?> GetDeferredChecks();

    Task<DtoDeferredSetupCheck?> AddOrUpdateDeferredCheck(DtoDeferredSetupCheck check);

    Task DeleteDeferredCheck(Guid checkId);
}
