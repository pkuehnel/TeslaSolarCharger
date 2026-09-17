using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Server.Services.HomeBatteryControl;

public class DtoHomeBatteryModeController
{
    public DtoHomeBatteryModeController(int templateConfigurationId, string name,
        Func<HomeBatteryMode, CancellationToken, Task> setModeAsync, TimeSpan? rewriteInterval)
    {
        TemplateConfigurationId = templateConfigurationId;
        Name = name;
        SetModeAsync = setModeAsync;
        RewriteInterval = rewriteInterval;
    }

    public int TemplateConfigurationId { get; }
    public string Name { get; }
    public Func<HomeBatteryMode, CancellationToken, Task> SetModeAsync { get; }
    /// <summary>
    /// If set, non normal modes need to be rewritten periodically as the device falls back to its default
    /// behavior when the external setpoint is not refreshed. This device side timeout also acts as failsafe
    /// in case TSC crashes while a non normal mode is active.
    /// </summary>
    public TimeSpan? RewriteInterval { get; }
}
