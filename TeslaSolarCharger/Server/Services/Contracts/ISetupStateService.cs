using TeslaSolarCharger.Shared.Dtos.Setup;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ISetupStateService
{
    /// <summary>
    /// The persisted setup state, migrated to the current schema, or null when setup has never been started or was
    /// already finished.
    /// </summary>
    Task<DtoSetupState?> GetSetupState();

    /// <summary>
    /// The persisted setup state, or a new one seeded from the installation's current configuration. Used when the
    /// assistant is opened so an existing installation starts from what is actually configured.
    /// </summary>
    Task<DtoSetupState> GetOrCreateSetupState();

    /// <summary>
    /// Adds a draft for every car that exists but is not part of this setup yet, and drops drafts whose car has
    /// been deleted. Called after an import, so cars that just arrived from a Tesla or SmartCar account appear in
    /// the setup list without the user having to start over.
    /// </summary>
    Task<DtoSetupState> SyncCarDrafts(DtoSetupState setupState);

    Task UpdateSetupState(DtoSetupState setupState);

    Task DeleteSetupState();
}
