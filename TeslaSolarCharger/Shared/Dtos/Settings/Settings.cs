using TeslaSolarCharger.Shared.Dtos.Contracts;

namespace TeslaSolarCharger.Shared.Dtos.Settings;

public class Settings : ISettings
{
    public bool IsNewVersionAvailable { get; set; }
    public int? InverterPower { get; set; }
    public int? Overage { get; set; }
    public int? PowerBuffer { get; set; }
    public List<DtoCar> CarsToManage => Cars.Where(c => c.ShouldBeManaged == true).OrderBy(c => c.ChargingPriority).ToList();
    public int? HomeBatterySoc { get; set; }
    public int? HomeBatteryPower { get; set; }
    public List<Issue> ActiveIssues { get; set; } = new();
    public bool ControlledACarAtLastCycle { get; set; }
    public DateTimeOffset LastPvValueUpdate { get; set; }
    public int? AverageHomeGridVoltage { get; set; }

    public bool CrashedOnStartup { get; set; }
    public string? StartupCrashMessage { get; set; }

    public bool FleetApiProxyNeeded { get; set; }

    public bool AllowUnlimitedFleetApiRequests { get; set; }
    public DateTime LastFleetApiRequestAllowedCheck { get; set; }
    public bool RestartNeeded { get; set; }

    public List<DtoCar> Cars { get; set; } = new();

    public Dictionary<int, string> RawRestRequestResults { get; set; } = new();
    public Dictionary<int, string> RawRestValues { get; set; } = new();
    public Dictionary<int, decimal?> CalculatedRestValues { get; set; } = new();

    public bool IsStartupCompleted { get; set; }

    public DateTime StartupTime { get; set; }

    public string? ChargePricesUpdateText { get; set; }
}
