using TeslaSolarCharger.Shared.Dtos.Setup;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ISetupApplicationService
{
    /// <summary>
    /// Writes the setup state to the real configuration without switching anything on: cars are saved unmanaged, so
    /// saving a setting during setup can never hand equipment to the charging scheduler. Reports per operation, so
    /// a failure halfway through can be retried without repeating what already worked.
    /// </summary>
    Task<DtoSetupApplicationResult> ApplyConfiguration(DtoSetupState setupState);

    /// <summary>
    /// Applies the configuration, then activates every piece of equipment that is ready and marks setup finished.
    /// Equipment that still misses something is left switched off. Setup is only marked complete, and its state only
    /// cleared, once every required save has actually succeeded.
    /// </summary>
    Task<DtoSetupApplicationResult> ActivateAndCompleteSetup(DtoSetupState setupState);

    /// <summary>
    /// Saves one car draft on its own, still unmanaged, and fills in the id of the row it was written to. Used as
    /// soon as a car's identity is complete, so everything that follows - a connection test, a charging deadline -
    /// has a real car to work with while the car itself stays switched off.
    /// </summary>
    Task<DtoSetupApplicationResult> SaveCarDraft(DtoSetupState setupState, Guid draftId);

    /// <summary>
    /// Writes the values the user accepted into the setup state and records that they were decided by the app, not
    /// entered by hand. Nothing is proposed straight into the live configuration: a proposal is shown first and only
    /// reaches the installation through the normal apply step.
    /// </summary>
    DtoSetupState AcceptProposals(DtoSetupState setupState, IReadOnlyCollection<DtoSetupProposedValue> proposals);
}
