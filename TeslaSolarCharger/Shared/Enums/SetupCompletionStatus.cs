namespace TeslaSolarCharger.Shared.Enums;

public enum SetupCompletionStatus
{
    NotStarted = 0,
    InProgress = 1,
    Complete = 2,
    /// <summary>The step does not apply to this installation, e.g. solar questions without a solar system.</summary>
    NotApplicable = 3,
}
