using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Setup;

/// <summary>
/// The task the user left setup for, e.g. a Tesla or SmartCar authorization that navigates the browser away.
/// Recorded before the redirect so the return lands on the exact task instead of the start of the assistant.
/// </summary>
public class DtoSetupPendingOperation
{
    /// <summary>
    /// Idempotency key echoed back by the return URL. A repeated callback with a key that was already handled must
    /// not import the same car twice.
    /// </summary>
    public Guid OperationId { get; set; } = Guid.NewGuid();

    public SetupStepKey StepKey { get; set; }

    /// <summary>Draft the operation belongs to, null for operations that are not about one device.</summary>
    public Guid? DraftId { get; set; }

    /// <summary>Free-form discriminator of what was started, e.g. "TeslaOAuth" or "SmartCarOAuth".</summary>
    public string? OperationName { get; set; }

    public DateTimeOffset StartedAt { get; set; }
}
