namespace TeslaSolarCharger.Shared.Dtos.Setup;

/// <summary>
/// The result of evaluating a setup state: which steps apply, what to do next, what the app proposes to configure
/// on its own, and what is still missing or contradictory. One shared decision model, so screens and server side
/// validation cannot disagree about what is required.
/// </summary>
public class DtoSetupDecision
{
    public List<DtoSetupStepStatus> Steps { get; set; } = new();

    /// <summary>Null when nothing is left to do.</summary>
    public DtoSetupNextAction? NextAction { get; set; }

    public List<DtoSetupProposedValue> ProposedValues { get; set; } = new();
    public List<DtoSetupIssue> MissingInformation { get; set; } = new();
    public List<DtoSetupIssue> Incompatibilities { get; set; } = new();
    public List<DtoSetupDeviceStatus> DeviceStatuses { get; set; } = new();

    /// <summary>
    /// True when every applicable step is complete. A postponed charging test or a sleeping car does not make this
    /// false: configuration completeness and verification are tracked separately.
    /// </summary>
    public bool IsConfigurationComplete { get; set; }

    /// <summary>
    /// True when everything the whole installation depends on is answered. Unlike <see cref="IsConfigurationComplete"/>
    /// a car or charging station that is not set up yet does not make this false: finishing leaves such equipment
    /// switched off, and it can be set up later.
    /// </summary>
    public bool CanFinishSetup { get; set; }
}
