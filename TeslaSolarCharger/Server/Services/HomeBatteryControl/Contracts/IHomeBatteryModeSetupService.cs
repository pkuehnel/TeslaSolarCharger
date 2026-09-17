namespace TeslaSolarCharger.Server.Services.HomeBatteryControl.Contracts;

public interface IHomeBatteryModeSetupService
{
    /// <summary>
    /// Returns one controller per template value configuration that has home battery control enabled.
    /// </summary>
    Task<List<DtoHomeBatteryModeController>> GetControllersAsync(CancellationToken cancellationToken);
}
