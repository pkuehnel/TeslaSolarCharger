using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Setup;

/// <summary>
/// Configuration, activation and connection state of one device, reported independently so a screen can truthfully
/// say that a device is configured but not enabled, or enabled but never tested.
/// </summary>
public class DtoSetupDeviceStatus
{
    public SetupDeviceKind DeviceKind { get; set; }
    public Guid? DraftId { get; set; }
    public int? DeviceId { get; set; }
    public string? DisplayName { get; set; }

    public SetupCompletionStatus ConfigurationStatus { get; set; }
    public SetupActivationStatus ActivationStatus { get; set; }
    public SetupCheckResultState ConnectionCheckState { get; set; }

    /// <summary>What has to be resolved before this device can be activated.</summary>
    public List<DtoSetupIssue> ActivationBlockers { get; set; } = new();
}
