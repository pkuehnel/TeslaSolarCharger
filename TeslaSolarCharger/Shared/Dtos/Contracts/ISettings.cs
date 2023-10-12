using TeslaSolarCharger.Shared.Dtos.Settings;

namespace TeslaSolarCharger.Shared.Dtos.Contracts;

public interface ISettings
{
    int? InverterPower { get; set; }
    int? Overage { get; set; }
    int? PowerBuffer { get; set; }
    List<Car> Cars { get; set; }
    List<Car> CarsToManage { get; }
    int? HomeBatterySoc { get; set; }
    int? HomeBatteryPower { get; set; }
    List<Issue> ActiveIssues { get; set; }
    bool ControlledACarAtLastCycle { get; set; }
    bool IsNewVersionAvailable { get; set; }
    DateTimeOffset LastPvValueUpdate { get; set; }
    int? AverageHomeGridVoltage { get; set; }
    int TeslaApiRequestCounter { get; set; }
}
