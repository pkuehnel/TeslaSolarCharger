namespace TeslaSolarCharger.Shared.Enums;

public enum SetupCheckResultState
{
    /// <summary>Never attempted. Not a failure: valid configuration can finish without a real charging test.</summary>
    NotRun = 0,
    Pending = 1,
    Succeeded = 2,
    Failed = 3,
}
