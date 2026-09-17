using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Setup;

/// <summary>
/// Records that one application step already succeeded. Kept in the setup state so retrying after a partial
/// failure skips completed work instead of repeating it.
/// </summary>
public class DtoSetupOperationRecord
{
    public SetupOperationKey OperationKey { get; set; }

    /// <summary>Draft the operation applied to, null for installation wide operations.</summary>
    public Guid? DraftId { get; set; }

    /// <summary>
    /// What was actually written, condensed. Skipping an operation is only safe while the answers have not changed
    /// since; without this a user who saves, edits and then finishes would keep the values they replaced. Null on a
    /// record written before this was tracked, which counts as "unknown" and makes the operation run again.
    /// </summary>
    public string? ContentHash { get; set; }

    public DateTimeOffset CompletedAt { get; set; }
}
