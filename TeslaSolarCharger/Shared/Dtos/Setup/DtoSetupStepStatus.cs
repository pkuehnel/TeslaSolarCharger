using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Setup;

public class DtoSetupStepStatus
{
    public SetupStepKey StepKey { get; set; }
    public bool IsApplicable { get; set; } = true;
    public SetupCompletionStatus Status { get; set; }

    /// <summary>Issues that keep this step from being complete.</summary>
    public List<DtoSetupIssue> Issues { get; set; } = new();
}
