namespace TeslaSolarCharger.Shared.Enums;

/// <summary>
/// Where a configured value came from. Needed to keep an explicit user decision from being overwritten by a newly
/// proposed default, and to label imported specifications with their provenance.
/// </summary>
public enum SetupValueSource
{
    /// <summary>No value known. Explicitly different from zero, false and "unsupported".</summary>
    Unknown = 0,
    UserEntered = 1,
    DerivedFromAnswers = 2,
    ImportedFromDevice = 3,
    ExistingConfiguration = 4,
    ApplicationDefault = 5,
}
