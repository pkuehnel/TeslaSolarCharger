using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Setup;

/// <summary>
/// A verification the user postponed, for example a real charging test on a car that was asleep during setup.
/// Stored outside the setup state so finishing setup does not erase it, and surfaced as a persistent dashboard
/// entry rather than a recurring notification.
/// </summary>
public class DtoDeferredSetupCheck
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DeferredSetupCheckKind Kind { get; set; }

    public SetupDeviceKind DeviceKind { get; set; }

    /// <summary>Database id of the device this check belongs to.</summary>
    public int? DeviceId { get; set; }

    public string? DisplayName { get; set; }

    public SetupCheckResultState State { get; set; } = SetupCheckResultState.NotRun;

    /// <summary>Result detail of the last attempt, null while the check has never run.</summary>
    public string? LastResultMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastAttemptedAt { get; set; }

    /// <summary>
    /// Fingerprint of the configuration the check result belongs to. When the configuration changes the stored
    /// result no longer describes what is configured, so the check is reset to <see cref="SetupCheckResultState.NotRun"/>.
    /// </summary>
    public string? ConfigurationFingerprint { get; set; }
}
