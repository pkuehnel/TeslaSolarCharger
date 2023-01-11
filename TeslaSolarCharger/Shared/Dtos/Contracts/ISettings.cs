using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Shared.Dtos.Contracts;

public interface ISettings
{
    double? InverterPower { get; set; }
    double? Overage { get; set; }
    List<Car> Cars { get; set; }
    double? HomeBatterySoc { get; set; }
    double? HomeBatteryPower { get; set; }
    List<Issue> ActiveIssues { get; set; }
    bool ControlledACarAtLastCycle { get; set; }
    bool IsNewVersionAvailable { get; set; }
}
