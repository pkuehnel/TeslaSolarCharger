using System.Collections.Concurrent;
using TeslaSolarCharger.Server.Services.HomeBatteryControl.Contracts;
using TeslaSolarCharger.Shared.Contracts;
using TeslaSolarCharger.Shared.Dtos.Contracts;
using TeslaSolarCharger.Shared.Dtos.HomeBatteryControl;
using TeslaSolarCharger.Shared.Dtos.Support;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.HomeBatteryControl;

/// <summary>
/// Needs to be a singleton as it tracks the mode that is currently applied to the home battery devices.
/// </summary>
public class HomeBatteryModeService : IHomeBatteryModeService
{
    private readonly ILogger<HomeBatteryModeService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISettings _settings;
    private readonly IConfigurationWrapper _configurationWrapper;

    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private HomeBatteryMode _currentMode = HomeBatteryMode.Unknown;
    private DateTimeOffset? _currentModeSetAt;
    private bool _modeWrittenSinceStartup;
    private HomeBatteryMode? _manualOverrideMode;
    private DateTimeOffset? _manualOverrideValidUntil;
    private readonly ConcurrentDictionary<int, DateTimeOffset> _lastSuccessfulWrites = new();
    private readonly ConcurrentDictionary<int, string> _lastErrors = new();

    public HomeBatteryModeService(ILogger<HomeBatteryModeService> logger,
        IServiceScopeFactory serviceScopeFactory,
        IDateTimeProvider dateTimeProvider,
        ISettings settings,
        IConfigurationWrapper configurationWrapper)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _dateTimeProvider = dateTimeProvider;
        _settings = settings;
        _configurationWrapper = configurationWrapper;
    }

    public async Task ApplyRequiredModeAsync(CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}()", nameof(ApplyRequiredModeAsync));
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ApplyRequiredModeInternalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SetManualModeAsync(HomeBatteryMode mode, TimeSpan validFor, CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}({mode}, {validFor})", nameof(SetManualModeAsync), mode, validFor);
        if (mode == HomeBatteryMode.Unknown)
        {
            throw new ArgumentException("Unknown is not a settable battery mode", nameof(mode));
        }
        if (validFor <= TimeSpan.Zero)
        {
            throw new ArgumentException("Duration must be positive", nameof(validFor));
        }
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _manualOverrideMode = mode;
            _manualOverrideValidUntil = _dateTimeProvider.DateTimeOffSetUtcNow().Add(validFor);
            await ApplyRequiredModeInternalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ClearManualModeAsync(CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}()", nameof(ClearManualModeAsync));
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _manualOverrideMode = default;
            _manualOverrideValidUntil = default;
            await ApplyRequiredModeInternalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<DtoHomeBatteryControlState> GetControlStateAsync(CancellationToken cancellationToken)
    {
        _logger.LogTrace("{method}()", nameof(GetControlStateAsync));
        var controllers = await GetControllersAsync(cancellationToken).ConfigureAwait(false);
        var now = _dateTimeProvider.DateTimeOffSetUtcNow();
        return new DtoHomeBatteryControlState
        {
            CurrentMode = _currentMode,
            CurrentModeSetAt = _currentModeSetAt,
            ManualOverrideMode = GetActiveOverrideMode(_manualOverrideMode, _manualOverrideValidUntil, now),
            ManualOverrideValidUntil = GetActiveOverrideMode(_manualOverrideMode, _manualOverrideValidUntil, now) == default
                ? default
                : _manualOverrideValidUntil,
            HomeBatterySoc = _settings.HomeBatterySoc,
            HomeBatteryPower = _settings.HomeBatteryPower,
            MaxChargeSoc = _configurationWrapper.HomeBatteryMaxChargeSoc(),
            AutomaticControlEnabled = _configurationWrapper.GridPriceBasedHomeBatteryControl(),
            PlannedWindows = _settings.HomeBatteryScheduleWindows.OrderBy(w => w.ValidFrom).ToList(),
            Controllers = controllers.Select(c => new DtoHomeBatteryControllerState
            {
                TemplateConfigurationId = c.TemplateConfigurationId,
                Name = c.Name,
                RequiresPeriodicRewrite = c.RewriteInterval != default,
                LastSuccessfulWrite = _lastSuccessfulWrites.TryGetValue(c.TemplateConfigurationId, out var lastWrite) ? lastWrite : default(DateTimeOffset?),
                LastError = _lastErrors.TryGetValue(c.TemplateConfigurationId, out var lastError) ? lastError : default,
            }).ToList(),
        };
    }

    public async Task RestoreNormalModeAsync()
    {
        _logger.LogTrace("{method}()", nameof(RestoreNormalModeAsync));
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            _manualOverrideMode = default;
            _manualOverrideValidUntil = default;
            if (_currentMode is not (HomeBatteryMode.Hold or HomeBatteryMode.Charge))
            {
                return;
            }
            var controllers = await GetControllersAsync(CancellationToken.None).ConfigureAwait(false);
            if (await WriteModeToControllersAsync(controllers, HomeBatteryMode.Normal, CancellationToken.None).ConfigureAwait(false))
            {
                _currentMode = HomeBatteryMode.Normal;
                _currentModeSetAt = _dateTimeProvider.DateTimeOffSetUtcNow();
                _modeWrittenSinceStartup = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while restoring normal home battery mode");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Determines the mode that needs to be written. Returns null if no write is required.
    /// While charging is required the battery soc is validated against the max charge soc and the mode is
    /// demoted to hold when the limit is reached so the battery can not be overcharged.
    /// </summary>
    /// <param name="modeWrittenSinceStartup">False until this process wrote a mode to the devices. As long as it is
    /// false <paramref name="currentMode"/> being unknown does not mean the devices are in their default behavior:
    /// a previous process might have left a hold or charge behind, e.g. when it was killed instead of shut down.</param>
    public static HomeBatteryMode? CalculateModeToWrite(HomeBatteryMode currentMode, HomeBatteryMode? requiredMode,
        int? homeBatterySoc, int maxChargeSoc, bool modeWrittenSinceStartup)
    {
        if (requiredMode == HomeBatteryMode.Charge && homeBatterySoc >= maxChargeSoc)
        {
            requiredMode = HomeBatteryMode.Hold;
        }
        if (requiredMode != null)
        {
            return requiredMode == currentMode ? null : requiredMode;
        }
        //Without any required mode the vendor default behavior needs to be restored, but only if it was modified before
        //or the mode a previous process left on the devices is still unknown.
        if (currentMode is HomeBatteryMode.Hold or HomeBatteryMode.Charge)
        {
            return HomeBatteryMode.Normal;
        }
        return currentMode == HomeBatteryMode.Unknown && !modeWrittenSinceStartup ? HomeBatteryMode.Normal : null;
    }

    public static HomeBatteryMode? GetActiveOverrideMode(HomeBatteryMode? overrideMode, DateTimeOffset? validUntil, DateTimeOffset now)
    {
        return overrideMode != null && validUntil > now ? overrideMode : null;
    }

    /// <summary>
    /// Determines the mode required by the planned schedule windows. Returns null when no window requires a mode.
    /// Windows with a SoC guard are only applied while the battery SoC is at or below the guard, so energy the battery
    /// does not need is not held back. Charge windows are demoted to hold once their target SoC is reached, so the
    /// bought energy is preserved but no unneeded energy is bought. While the home battery is actively discharged into
    /// cars, no window is applied as that would contradict the discharging.
    /// </summary>
    public static HomeBatteryMode? CalculateAutomaticMode(IEnumerable<DtoHomeBatteryScheduleWindow> scheduleWindows,
        DateTimeOffset now, int? homeBatterySoc, bool isHomeBatteryDischargingActive)
    {
        if (isHomeBatteryDischargingActive)
        {
            return null;
        }
        var activeWindows = scheduleWindows
            .Where(w => w.ValidFrom <= now && w.ValidTo > now)
            .Where(w => w.OnlyWhileSocAtOrBelowPercent == default
                        || (homeBatterySoc != default && homeBatterySoc <= w.OnlyWhileSocAtOrBelowPercent))
            .ToList();
        var chargeWindow = activeWindows.FirstOrDefault(w => w.Mode == HomeBatteryMode.Charge);
        if (chargeWindow != default)
        {
            if (homeBatterySoc >= chargeWindow.TargetSocPercent)
            {
                return HomeBatteryMode.Hold;
            }
            return HomeBatteryMode.Charge;
        }
        return activeWindows.Any(w => w.Mode == HomeBatteryMode.Hold) ? HomeBatteryMode.Hold : null;
    }

    private async Task ApplyRequiredModeInternalAsync(CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.DateTimeOffSetUtcNow();
        if (_manualOverrideMode != default && GetActiveOverrideMode(_manualOverrideMode, _manualOverrideValidUntil, now) == default)
        {
            _logger.LogInformation("Manual home battery mode override {mode} expired", _manualOverrideMode);
            _manualOverrideMode = default;
            _manualOverrideValidUntil = default;
        }
        var controllers = await GetControllersAsync(cancellationToken).ConfigureAwait(false);
        if (controllers.Count == 0)
        {
            _currentMode = HomeBatteryMode.Unknown;
            _currentModeSetAt = default;
            return;
        }
        var requiredMode = GetActiveOverrideMode(_manualOverrideMode, _manualOverrideValidUntil, now);
        if (requiredMode == default && _configurationWrapper.GridPriceBasedHomeBatteryControl())
        {
            requiredMode = CalculateAutomaticMode(_settings.HomeBatteryScheduleWindows, now, _settings.HomeBatterySoc,
                _settings.IsHomeBatteryDischargingActive);
            if (requiredMode != default)
            {
                _logger.LogTrace("Automatic home battery mode from planned schedule windows: {mode}", requiredMode);
            }
        }
        var maxChargeSoc = _configurationWrapper.HomeBatteryMaxChargeSoc();
        if (requiredMode == HomeBatteryMode.Charge && _settings.HomeBatterySoc == default)
        {
            _logger.LogWarning("Home battery soc is unknown, can not validate max charge soc while charge mode is active");
        }
        if (requiredMode == HomeBatteryMode.Charge && _settings.HomeBatterySoc >= maxChargeSoc)
        {
            _logger.LogInformation("Home battery charge mode is demoted to hold as soc {soc} reached max charge soc {maxChargeSoc}",
                _settings.HomeBatterySoc, maxChargeSoc);
        }
        var modeToWrite = CalculateModeToWrite(_currentMode, requiredMode, _settings.HomeBatterySoc, maxChargeSoc,
            _modeWrittenSinceStartup);
        if (modeToWrite != default)
        {
            if (!_modeWrittenSinceStartup && modeToWrite == HomeBatteryMode.Normal)
            {
                _logger.LogInformation("Restoring normal home battery mode after startup as a previous process might have left the devices in hold or charge mode");
            }
            _logger.LogInformation("Setting home battery mode to {mode}", modeToWrite);
            if (await WriteModeToControllersAsync(controllers, modeToWrite.Value, cancellationToken).ConfigureAwait(false))
            {
                _currentMode = modeToWrite.Value;
                _currentModeSetAt = now;
                _modeWrittenSinceStartup = true;
            }
            return;
        }
        if (_currentMode is HomeBatteryMode.Hold or HomeBatteryMode.Charge)
        {
            await RewriteModeOnControllersRequiringPeriodicRewrite(controllers, now, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RewriteModeOnControllersRequiringPeriodicRewrite(List<DtoHomeBatteryModeController> controllers,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var controller in controllers)
        {
            if (controller.RewriteInterval == default)
            {
                continue;
            }
            //Rewrite after half the interval so a single failed write does not lead to the device falling back to default behavior.
            var rewriteDue = !_lastSuccessfulWrites.TryGetValue(controller.TemplateConfigurationId, out var lastWrite)
                             || now - lastWrite >= controller.RewriteInterval.Value / 2;
            if (!rewriteDue)
            {
                continue;
            }
            await WriteModeToControllerAsync(controller, _currentMode, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> WriteModeToControllersAsync(List<DtoHomeBatteryModeController> controllers, HomeBatteryMode mode,
        CancellationToken cancellationToken)
    {
        var allSucceeded = true;
        foreach (var controller in controllers)
        {
            if (!await WriteModeToControllerAsync(controller, mode, cancellationToken).ConfigureAwait(false))
            {
                allSucceeded = false;
            }
        }
        return allSucceeded;
    }

    private async Task<bool> WriteModeToControllerAsync(DtoHomeBatteryModeController controller, HomeBatteryMode mode,
        CancellationToken cancellationToken)
    {
        try
        {
            await controller.SetModeAsync(mode, cancellationToken).ConfigureAwait(false);
            _lastSuccessfulWrites[controller.TemplateConfigurationId] = _dateTimeProvider.DateTimeOffSetUtcNow();
            _lastErrors.TryRemove(controller.TemplateConfigurationId, out _);
            _logger.LogDebug("Set home battery mode {mode} on {controllerName}", mode, controller.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while setting home battery mode {mode} on {controllerName}", mode, controller.Name);
            _lastErrors[controller.TemplateConfigurationId] = ex.Message;
            return false;
        }
    }

    private async Task<List<DtoHomeBatteryModeController>> GetControllersAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var setupServices = scope.ServiceProvider.GetServices<IHomeBatteryModeSetupService>();
        var controllers = new List<DtoHomeBatteryModeController>();
        foreach (var setupService in setupServices)
        {
            try
            {
                controllers.AddRange(await setupService.GetControllersAsync(cancellationToken).ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting home battery controllers from {setupService}", setupService.GetType().Name);
            }
        }
        return controllers;
    }
}
