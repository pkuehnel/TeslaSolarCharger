using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Setup;

/// <summary>Outcome of one application step, so a partial save can never be reported as a success.</summary>
public class DtoSetupOperationResult
{
    public SetupOperationKey OperationKey { get; set; }

    /// <summary>Draft the operation applied to, null for installation wide operations.</summary>
    public Guid? DraftId { get; set; }

    public bool IsSuccess { get; set; }

    /// <summary>True when the operation was skipped because a previous run already completed it.</summary>
    public bool WasAlreadyCompleted { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>Whether retrying the same operation can succeed without the user changing anything.</summary>
    public bool IsRetryable { get; set; }
}
