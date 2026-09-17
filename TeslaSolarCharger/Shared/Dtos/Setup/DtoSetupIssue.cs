using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Setup;

/// <summary>
/// Something the user still has to resolve, described so a screen can show it without knowing which internal
/// property it maps to.
/// </summary>
public class DtoSetupIssue
{
    public SetupIssueSeverity Severity { get; set; }

    /// <summary>Translation key of the explanation. Never a raw internal property name.</summary>
    public string MessageKey { get; set; } = string.Empty;

    /// <summary>Step the user has to go to in order to resolve this.</summary>
    public SetupStepKey StepKey { get; set; }

    /// <summary>Draft the issue belongs to, null for installation wide issues.</summary>
    public Guid? DraftId { get; set; }

    /// <summary>Configuration property the issue is about. For diagnostics and tests, not for display.</summary>
    public string? PropertyName { get; set; }
}
