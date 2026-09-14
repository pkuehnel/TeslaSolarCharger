namespace TeslaSolarCharger.Shared.Enums;

public enum SetupIssueSeverity
{
    /// <summary>Worth explaining, but nothing is blocked.</summary>
    Information = 0,
    /// <summary>A required value is not known yet. The configuration is incomplete, not invalid.</summary>
    MissingInformation = 1,
    /// <summary>Two chosen settings contradict each other and cannot both be saved.</summary>
    Incompatible = 2,
}
