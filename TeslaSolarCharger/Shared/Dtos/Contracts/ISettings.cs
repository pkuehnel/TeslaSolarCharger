using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Shared.Dtos.Contracts;

public interface ISettings
{
    int? InverterPower { get; set; }
    int? Overage { get; set; }
    int? PowerBuffer { get; set; }
    int? HomeBatterySoc { get; set; }
    int? HomeBatteryPower { get; set; }
    List<Issue> ActiveIssues { get; set; }
    bool ControlledACarAtLastCycle { get; set; }
    bool IsNewVersionAvailable { get; set; }
    DateTimeOffset LastPvValueUpdate { get; set; }
    int? AverageHomeGridVoltage { get; set; }
    int TeslaApiRequestCounter { get; set; }
    bool CrashedOnStartup { get; set; }
    string? StartupCrashMessage { get; set; }
    bool FleetApiProxyNeeded { get; set; }
    bool AllowUnlimitedFleetApiRequests { get; set; }
    DateTime LastFleetApiRequestAllowedCheck { get; set; }
    List<DtoCar> Cars { get; set; }
    List<DtoCar> CarsToManage { get; }
    bool RestartNeeded { get; set; }
    Dictionary<int, string> RawRestRequestResults { get; set; }
    Dictionary<int, string> RawRestValues { get; set; }
    Dictionary<int, decimal> CalculatedRestValues { get; set; }
}
