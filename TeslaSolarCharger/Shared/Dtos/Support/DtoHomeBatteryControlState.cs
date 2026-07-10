using TeslaSolarCharger.Shared.Dtos.HomeBatteryControl;
using TeslaSolarCharger.SharedModel.Enums;

namespace TeslaSolarCharger.Shared.Dtos.Support;

public class DtoHomeBatteryControlState
{
    public HomeBatteryMode CurrentMode { get; set; }
    public DateTimeOffset? CurrentModeSetAt { get; set; }
    public HomeBatteryMode? ManualOverrideMode { get; set; }
    public DateTimeOffset? ManualOverrideValidUntil { get; set; }
    public int? HomeBatterySoc { get; set; }
    public int? HomeBatteryPower { get; set; }
    public int MaxChargeSoc { get; set; }
    public bool AutomaticControlEnabled { get; set; }
    public List<DtoHomeBatteryScheduleWindow> PlannedWindows { get; set; } = new();
    public List<DtoHomeBatteryControllerState> Controllers { get; set; } = new();
}

public class DtoHomeBatteryControllerState
{
    public int TemplateConfigurationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool RequiresPeriodicRewrite { get; set; }
    public DateTimeOffset? LastSuccessfulWrite { get; set; }
    public string? LastError { get; set; }
}
