using Newtonsoft.Json;
using TeslaSolarCharger.Server.Services.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Setup;
using TeslaSolarCharger.Shared.Enums;
using TeslaSolarCharger.Shared.Resources.Contracts;

namespace TeslaSolarCharger.Server.Services;

public class DeferredSetupCheckService(
    ILogger<DeferredSetupCheckService> logger,
    ITscConfigurationService tscConfigurationService,
    IDateTimeProvider dateTimeProvider,
    IConstants constants)
    : IDeferredSetupCheckService
{
    public async Task<List<DtoDeferredSetupCheck>> GetDeferredChecks()
    {
        logger.LogTrace("{method}()", nameof(GetDeferredChecks));
        var json = await tscConfigurationService.GetConfigurationValueByKey(constants.DeferredSetupChecksKey).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<DtoDeferredSetupCheck>();
        }

        try
        {
            return JsonConvert.DeserializeObject<List<DtoDeferredSetupCheck>>(json) ?? new List<DtoDeferredSetupCheck>();
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Stored deferred setup checks could not be read and are ignored.");
            return new List<DtoDeferredSetupCheck>();
        }
    }

    public async Task<DtoDeferredSetupCheck> AddOrUpdateDeferredCheck(DtoDeferredSetupCheck check)
    {
        logger.LogTrace("{method}({kind} for {deviceKind} {deviceId})", nameof(AddOrUpdateDeferredCheck), check.Kind, check.DeviceKind, check.DeviceId);
        var checks = await GetDeferredChecks().ConfigureAwait(false);
        var existing = checks.FirstOrDefault(c => c.Id == check.Id)
                       ?? checks.FirstOrDefault(c => c.Kind == check.Kind
                                                     && c.DeviceKind == check.DeviceKind
                                                     && c.DeviceId == check.DeviceId);
        if (existing == null)
        {
            check.CreatedAt = dateTimeProvider.DateTimeOffSetUtcNow();
            checks.Add(check);
            await PersistChecks(checks).ConfigureAwait(false);
            return check;
        }

        existing.DisplayName = check.DisplayName;
        existing.State = check.State;
        existing.LastResultMessage = check.LastResultMessage;
        existing.ConfigurationFingerprint = check.ConfigurationFingerprint;
        await PersistChecks(checks).ConfigureAwait(false);
        return existing;
    }

    public async Task RecordCheckResult(Guid checkId, SetupCheckResultState state, string? message)
    {
        logger.LogTrace("{method}({checkId}, {state})", nameof(RecordCheckResult), checkId, state);
        var checks = await GetDeferredChecks().ConfigureAwait(false);
        var check = checks.FirstOrDefault(c => c.Id == checkId);
        if (check == null)
        {
            logger.LogWarning("No deferred setup check with id {checkId} to record a result for.", checkId);
            return;
        }

        check.State = state;
        check.LastResultMessage = message;
        check.LastAttemptedAt = dateTimeProvider.DateTimeOffSetUtcNow();
        await PersistChecks(checks).ConfigureAwait(false);
    }

    public async Task RemoveDeferredCheck(Guid checkId)
    {
        logger.LogTrace("{method}({checkId})", nameof(RemoveDeferredCheck), checkId);
        var checks = await GetDeferredChecks().ConfigureAwait(false);
        var removed = checks.RemoveAll(c => c.Id == checkId);
        if (removed > 0)
        {
            await PersistChecks(checks).ConfigureAwait(false);
        }
    }

    public async Task ReevaluateChecksForDevice(SetupDeviceKind deviceKind, int deviceId, string configurationFingerprint)
    {
        logger.LogTrace("{method}({deviceKind}, {deviceId})", nameof(ReevaluateChecksForDevice), deviceKind, deviceId);
        var checks = await GetDeferredChecks().ConfigureAwait(false);
        var affected = checks
            .Where(c => c.DeviceKind == deviceKind
                        && c.DeviceId == deviceId
                        && !string.Equals(c.ConfigurationFingerprint, configurationFingerprint, StringComparison.Ordinal))
            .ToList();
        if (affected.Count == 0)
        {
            return;
        }

        foreach (var check in affected)
        {
            //The stored result described a configuration that no longer exists. Keeping it would tell the user
            //their new setup was already verified.
            check.State = SetupCheckResultState.NotRun;
            check.LastResultMessage = null;
            check.LastAttemptedAt = null;
            check.ConfigurationFingerprint = configurationFingerprint;
        }

        await PersistChecks(checks).ConfigureAwait(false);
    }

    private async Task PersistChecks(List<DtoDeferredSetupCheck> checks)
    {
        var json = JsonConvert.SerializeObject(checks);
        await tscConfigurationService.SetConfigurationValueByKey(constants.DeferredSetupChecksKey, json).ConfigureAwait(false);
    }
}
