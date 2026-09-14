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

    public DateTimeOffset CompletedAt { get; set; }
}
