using TeslaSolarCharger.Shared.Dtos.Setup;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface ISetupStateMigrator
{
    /// <summary>
    /// Turns a persisted setup state of any schema version into the current shape. Returns null when nothing was
    /// persisted or the stored text cannot be read at all; never throws away answers it can still interpret.
    /// </summary>
    DtoSetupState? Migrate(string? persistedJson);
}
