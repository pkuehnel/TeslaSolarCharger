namespace TeslaSolarCharger.Shared.Enums;

/// <summary>
/// Whether a device may already be used by the charging scheduler. Configuration status, activation status and
/// connection/test status are tracked independently: every device in setup is switched on when setup finishes, so
/// a fully configured one is <see cref="ReadyForActivation"/> until then.
/// </summary>
public enum SetupActivationStatus
{
    /// <summary>Not evaluated yet.</summary>
    Draft = 0,
    /// <summary>All prerequisites are met; the explicit activation action would enable it.</summary>
    ReadyForActivation = 1,
    /// <summary>Already active. Activating an existing installation's equipment again must not disturb it.</summary>
    Active = 2,
    /// <summary>Cannot be activated yet; see the reported blockers.</summary>
    Blocked = 3,
}
