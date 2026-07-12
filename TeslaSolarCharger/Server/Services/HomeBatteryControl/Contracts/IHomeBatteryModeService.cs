using TeslaSolarCharger.Shared.Dtos.Support;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.HomeBatteryControl.Contracts;

public interface IHomeBatteryModeService
{
    /// <summary>
    /// Determines the currently required battery mode and writes it to all configured controllers.
    /// Modes are only written on transitions, except for controllers that require periodic rewrites.
    /// </summary>
    Task ApplyRequiredModeAsync(CancellationToken cancellationToken);
    Task SetManualModeAsync(HomeBatteryMode mode, TimeSpan validFor, CancellationToken cancellationToken);
    Task ClearManualModeAsync(CancellationToken cancellationToken);
    Task<DtoHomeBatteryControlState> GetControlStateAsync(CancellationToken cancellationToken);
    /// <summary>
    /// Best effort restore of normal mode, e.g. on application shutdown.
    /// </summary>
    Task RestoreNormalModeAsync();
}
