using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Setup;

/// <summary>The single thing the user should do next.</summary>
public class DtoSetupNextAction
{
    public SetupStepKey StepKey { get; set; }
    public Guid? DraftId { get; set; }

    /// <summary>Translation key of the action label.</summary>
    public string DescriptionKey { get; set; } = string.Empty;
}
