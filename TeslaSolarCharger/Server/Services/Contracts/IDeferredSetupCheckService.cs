using TeslaSolarCharger.Shared.Dtos.Setup;
using TeslaSolarCharger.Shared.Enums;

namespace TeslaSolarCharger.Server.Services.Contracts;

public interface IDeferredSetupCheckService
{
    Task<List<DtoDeferredSetupCheck>> GetDeferredChecks();

    /// <summary>
    /// Records that a verification is outstanding. Adding the same check for the same device twice updates the
    /// existing entry, so a user who postpones a charging test repeatedly does not collect duplicate reminders.
    /// </summary>
    Task<DtoDeferredSetupCheck> AddOrUpdateDeferredCheck(DtoDeferredSetupCheck check);

    Task RecordCheckResult(Guid checkId, SetupCheckResultState state, string? message);

    Task RemoveDeferredCheck(Guid checkId);

    /// <summary>
    /// Resets the stored result of every check for a device whose configuration no longer matches the one the
    /// result was recorded against, because a result from before the change says nothing about what is configured
    /// now.
    /// </summary>
    Task ReevaluateChecksForDevice(SetupDeviceKind deviceKind, int deviceId, string configurationFingerprint);
}
